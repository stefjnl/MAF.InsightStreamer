using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MAF.InsightStreamer.Application.Configuration;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Exceptions;
using MAF.InsightStreamer.Domain.Models;
using System.Threading;

namespace MAF.InsightStreamer.Application.Services;

/// <summary>
/// Service for handling conversational Q&A about analyzed documents.
/// Orchestrates between document sessions, thread management, and content analysis services.
/// </summary>
public class QuestionAnswerService : IQuestionAnswerService
{
    private readonly IDocumentSessionService _documentSessionService;
    private readonly IThreadManagementService _threadManagementService;
    private readonly IContentOrchestratorService _contentOrchestratorService;
    private readonly ILogger<QuestionAnswerService> _logger;
    private readonly QuestionAnswerSettings _settings;

    /// <summary>
    /// Initializes a new instance of the QuestionAnswerService class.
    /// </summary>
    /// <param name="documentSessionService">Service for managing document sessions.</param>
    /// <param name="threadManagementService">Service for managing conversation threads.</param>
    /// <param name="contentOrchestratorService">Service for orchestrating content analysis.</param>
    /// <param name="logger">Logger for logging operations and errors.</param>
    /// <param name="settings">Configuration settings for question answering.</param>
    public QuestionAnswerService(
        IDocumentSessionService documentSessionService,
        IThreadManagementService threadManagementService,
        IContentOrchestratorService contentOrchestratorService,
        ILogger<QuestionAnswerService> logger,
        IOptions<QuestionAnswerSettings> settings)
    {
        _documentSessionService = documentSessionService ?? throw new ArgumentNullException(nameof(documentSessionService));
        _threadManagementService = threadManagementService ?? throw new ArgumentNullException(nameof(threadManagementService));
        _contentOrchestratorService = contentOrchestratorService ?? throw new ArgumentNullException(nameof(contentOrchestratorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public async Task<QuestionAnswerResult> AskQuestionAsync(
        Guid sessionId,
        string question,
        string? threadId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing question for session {SessionId}, thread {ThreadId}", sessionId, threadId);

        try
        {
            // Validate sessionId exists and retrieve DocumentSession
            var documentSession = await _documentSessionService.GetSessionAsync(sessionId, cancellationToken);
            if (documentSession == null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                throw new SessionNotFoundException(sessionId);
            }

            // Check if session has expired
            if (documentSession.IsExpired)
            {
                _logger.LogWarning("Session {SessionId} expired on {ExpiresAt}", sessionId, documentSession.ExpiresAt);
                throw new SessionExpiredException(sessionId, documentSession.ExpiresAt);
            }

            // Check rate limiting - question count
            var userQuestionCount = documentSession.ConversationHistory.Count(m => m.Role == MessageRole.User);
            if (userQuestionCount >= _settings.MaxQuestionsPerSession)
            {
                _logger.LogWarning("Question rate limit exceeded for session {SessionId}. Current: {Current}, Max: {Max}",
                    sessionId, userQuestionCount, _settings.MaxQuestionsPerSession);
                throw new RateLimitExceededException(sessionId, _settings.MaxQuestionsPerSession, userQuestionCount);
            }

            // Check rate limiting - token usage
            var estimatedTokensForThisQuestion = _settings.EstimatedTokensPerQuestion + _settings.EstimatedTokensPerAnswer;
            var estimatedTotalTokensAfterThisQuestion = documentSession.TotalTokensUsed + estimatedTokensForThisQuestion;
            
            if (estimatedTotalTokensAfterThisQuestion > _settings.MaxTokensPerSession)
            {
                _logger.LogWarning("Token rate limit exceeded for session {SessionId}. Current: {Current:N0}, Estimated after question: {Estimated:N0}, Max: {Max:N0}",
                    sessionId, documentSession.TotalTokensUsed, estimatedTotalTokensAfterThisQuestion, _settings.MaxTokensPerSession);
                throw new RateLimitExceededException(sessionId, _settings.MaxTokensPerSession, documentSession.TotalTokensUsed);
            }

            // Handle thread management
            string actualThreadId;
            if (string.IsNullOrEmpty(threadId))
            {
                // Create new thread for this session
                actualThreadId = await _threadManagementService.CreateThreadForDocumentAsync(sessionId, cancellationToken);
                _logger.LogInformation("Created new thread {ThreadId} for session {SessionId}", actualThreadId, sessionId);
            }
            else
            {
                // Validate existing thread matches the session
                var existingThread = await _threadManagementService.GetThreadAsync(threadId, cancellationToken);
                if (existingThread == null)
                {
                    _logger.LogWarning("Thread {ThreadId} not found", threadId);
                    throw new ThreadIdMismatchException(threadId, sessionId, null);
                }

                if (existingThread.SessionId != sessionId)
                {
                    _logger.LogWarning("Thread {ThreadId} belongs to session {ActualSessionId}, not {ExpectedSessionId}",
                        threadId, existingThread.SessionId, sessionId);
                    throw new ThreadIdMismatchException(threadId, sessionId, existingThread.SessionId);
                }

                actualThreadId = threadId;
            }

            // Retrieve conversation history from DocumentSession
            var conversationHistory = documentSession.ConversationHistory.ToList();

            // Log which provider will be used for this request
            var currentProvider = _contentOrchestratorService.GetCurrentProviderConfiguration();
            _logger.LogDebug("Calling orchestrator with {Provider}:{Model} for question in session {SessionId}, thread {ThreadId}",
                currentProvider.Provider, currentProvider.Model, sessionId, actualThreadId);

            // Call orchestrator's AskQuestionAsync
            var orchestratorResponse = await _contentOrchestratorService.AskQuestionAsync(
                question,
                documentSession.DocumentChunks,
                actualThreadId,
                conversationHistory,
                cancellationToken);

            // Estimate actual tokens used and update session
            var actualTokensUsed = EstimateTokensFromText(question) + EstimateTokensFromText(orchestratorResponse);
            documentSession.TotalTokensUsed += actualTokensUsed;

            // Parse orchestrator response (JSON with answer + chunk indices)
            string answer;
            List<int> relevantChunkIndices;
            try
            {
                _logger.LogDebug("Attempting to parse orchestrator response: {Response}", orchestratorResponse);
                
                var responseJson = JsonSerializer.Deserialize<JsonElement>(orchestratorResponse);
                
                // Validate required properties exist
                if (!responseJson.TryGetProperty("answer", out var answerElement))
                {
                    throw new JsonException("Missing required property 'answer' in JSON response");
                }
                
                if (!responseJson.TryGetProperty("relevantChunks", out var chunksElement))
                {
                    throw new JsonException("Missing required property 'relevantChunks' in JSON response");
                }
                
                answer = answerElement.GetString() ?? string.Empty;
                
                // Handle both "relevantChunks" and "relevantChunkIndices" property names
                var chunkProperty = responseJson.TryGetProperty("relevantChunkIndices", out var chunkIndicesElement)
                    ? chunkIndicesElement
                    : chunksElement;
                
                relevantChunkIndices = new List<int>();
                if (chunkProperty.ValueKind == JsonValueKind.Array)
                {
                    relevantChunkIndices = chunkProperty
                        .EnumerateArray()
                        .Where(element => element.ValueKind == JsonValueKind.Number)
                        .Select(element => element.GetInt32())
                        .ToList();
                }
                
                _logger.LogInformation("Successfully parsed JSON response with {ChunkCount} relevant chunks", relevantChunkIndices.Count);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse orchestrator response as JSON. Response length: {ResponseLength}, Response preview: {ResponsePreview}",
                    orchestratorResponse.Length, orchestratorResponse.Substring(0, Math.Min(200, orchestratorResponse.Length)));
                
                // Enhanced fallback: try to extract JSON from markdown or other formatting
                var cleanedResponse = CleanJsonResponse(orchestratorResponse);
                if (cleanedResponse != orchestratorResponse)
                {
                    try
                    {
                        var responseJson = JsonSerializer.Deserialize<JsonElement>(cleanedResponse);
                        answer = responseJson.GetProperty("answer").GetString() ?? cleanedResponse;
                        relevantChunkIndices = new List<int>();
                        _logger.LogInformation("Successfully parsed cleaned JSON response");
                    }
                    catch (JsonException cleanEx)
                    {
                        _logger.LogWarning(cleanEx, "Failed to parse cleaned JSON response as well");
                        // Final fallback: treat entire response as answer with no chunk references
                        answer = orchestratorResponse;
                        relevantChunkIndices = new List<int>();
                    }
                }
                else
                {
                    // Fallback: treat entire response as answer with no chunk references
                    answer = orchestratorResponse;
                    relevantChunkIndices = new List<int>();
                }
            }

            // Create ConversationMessage for user question
            var userMessage = new ConversationMessage(MessageRole.User, question);
            documentSession.ConversationHistory.Add(userMessage);

            // Create ConversationMessage for assistant answer
            var assistantMessage = new ConversationMessage(MessageRole.Assistant, answer, relevantChunkIndices);
            documentSession.ConversationHistory.Add(assistantMessage);

            // Update DocumentSession with new messages and token usage
            await _documentSessionService.UpdateSessionExpirationAsync(sessionId, cancellationToken);

            // Refresh session expiration and log token usage
            _logger.LogInformation("Updated expiration for session {SessionId}. Total tokens used: {TotalTokens:N0}",
                sessionId, documentSession.TotalTokensUsed);

            // Build and return QuestionAnswerResult
            var result = new QuestionAnswerResult(
                answer,
                relevantChunkIndices,
                documentSession.ConversationHistory.ToList(),
                actualThreadId);

            _logger.LogInformation("Successfully processed question for session {SessionId}, thread {ThreadId} with {Provider}:{Model}",
                sessionId, actualThreadId, currentProvider.Provider, currentProvider.Model);
            return result;
        }
        catch (SessionNotFoundException)
        {
            // Re-throw custom exceptions as-is
            throw;
        }
        catch (SessionExpiredException)
        {
            // Re-throw custom exceptions as-is
            throw;
        }
        catch (ThreadIdMismatchException)
        {
            // Re-throw custom exceptions as-is
            throw;
        }
        catch (RateLimitExceededException)
        {
            // Re-throw custom exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            var currentProvider = _contentOrchestratorService.GetCurrentProviderConfiguration();
            _logger.LogError(ex, "Error processing question for session {SessionId}, thread {ThreadId} with {Provider}:{Model}",
                sessionId, threadId, currentProvider.Provider, currentProvider.Model);
            throw new InvalidOperationException("An error occurred while processing your question. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Cleans JSON response by removing markdown formatting, comments, and other non-JSON content.
    /// </summary>
    /// <param name="response">The raw response that may contain JSON.</param>
    /// <returns>Cleaned JSON string or original response if no cleaning was possible.</returns>
    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        try
        {
            // Look for JSON object boundaries
            var startIndex = response.IndexOf('{');
            var endIndex = response.LastIndexOf('}');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                var jsonContent = response.Substring(startIndex, endIndex - startIndex + 1);
                
                // Remove common markdown formatting
                jsonContent = jsonContent.Replace("```json", "").Replace("```", "");
                
                // Remove HTML comments and other common formatting issues
                jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, @"<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
                
                // Try to parse the cleaned content
                JsonSerializer.Deserialize<JsonElement>(jsonContent);
                _logger.LogDebug("Successfully cleaned JSON response");
                return jsonContent;
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clean JSON response, returning original");
            return response;
        }
    }

    /// <summary>
    /// Estimates the number of tokens in a text string.
    /// This is a rough approximation - actual tokenization depends on the model used.
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>Estimated number of tokens.</returns>
    private static int EstimateTokensFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Rough estimation: approximately 4 characters per token for English text
        // This is a conservative estimate that works reasonably well for most models
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}