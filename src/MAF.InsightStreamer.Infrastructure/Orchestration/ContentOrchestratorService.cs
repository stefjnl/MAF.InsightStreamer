using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Providers;

namespace MAF.InsightStreamer.Infrastructure.Orchestration;

public class ContentOrchestratorService : IContentOrchestratorService
{
    private readonly IYouTubeService _youtubeService;
    private readonly IChunkingService _chunkingService;
    private readonly IThreadManagementService _threadManagementService;
    private readonly AIAgent _orchestrator;
    private readonly ILogger<ContentOrchestratorService> _logger;
    
    // Cache for video data with 5-minute expiration
    private readonly ConcurrentDictionary<string, (VideoMetadata metadata, List<TranscriptChunk> transcript, DateTime timestamp)> _videoCache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    
    // Thread cache at orchestrator level to keep threads warm during service lifetime
    private readonly Dictionary<string, object> _activeThreads = new();

    public ContentOrchestratorService(
        IOptions<ProviderSettings> settings,
        IYouTubeService youtubeService,
        IChunkingService chunkingService,
        IThreadManagementService threadManagementService,
        ILogger<ContentOrchestratorService> logger)
    {
        _youtubeService = youtubeService;
        _chunkingService = chunkingService;
        _threadManagementService = threadManagementService;
        _logger = logger;
        _videoCache = new ConcurrentDictionary<string, (VideoMetadata, List<TranscriptChunk>, DateTime)>();

        var config = settings.Value;

        // Create ChatClient
        ChatClient chatClient = new(
            model: config.Model,
            credential: new ApiKeyCredential(config.ApiKey),
            options: new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(config.Endpoint)
            }
        );

        // Convert to IChatClient
        IChatClient client = chatClient.AsIChatClient();

        // Create orchestrator with ALL tools registered
        _orchestrator = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                Name = "ContentOrchestratorAgent",
                Instructions = "You coordinate content analysis workflows. Use available tools to extract, chunk, and summarize content.",
                ChatOptions = new ChatOptions
                {
                    Tools = [
                        AIFunctionFactory.Create(ExtractYouTubeVideo),
                        AIFunctionFactory.Create(ChunkTranscriptForAnalysis),
                        AIFunctionFactory.Create(SummarizeVideo),
                        AIFunctionFactory.Create(AnswerQuestionAboutDocument)
                    ]
                }
            }
        );
    }

    /// <summary>
    /// Gets video data (metadata and transcript) with caching to avoid duplicate service calls.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL</param>
    /// <returns>A tuple containing video metadata and transcript chunks</returns>
    private async Task<(VideoMetadata metadata, List<TranscriptChunk> transcript)> GetVideoDataAsync(string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            throw new ArgumentException("Video URL cannot be null or empty", nameof(videoUrl));
        }

        // Check cache first
        if (_videoCache.TryGetValue(videoUrl, out var cachedData))
        {
            // Check if cache is still valid
            if (DateTime.UtcNow - cachedData.timestamp < _cacheExpiration)
            {
                _logger.LogInformation("Cache HIT for video URL: {VideoUrl}", videoUrl);
                return (cachedData.metadata, cachedData.transcript);
            }
            else
            {
                // Remove expired entry
                _videoCache.TryRemove(videoUrl, out _);
                _logger.LogInformation("Cache EXPIRED for video URL: {VideoUrl}", videoUrl);
            }
        }

        _logger.LogInformation("Cache MISS for video URL: {VideoUrl}. Fetching from YouTube service.", videoUrl);

        // Fetch from YouTube service
        var metadata = await _youtubeService.GetVideoMetadataAsync(videoUrl);
        var transcript = await _youtubeService.GetTranscriptAsync(videoUrl);

        // Store in cache
        _videoCache.TryAdd(videoUrl, (metadata, transcript, DateTime.UtcNow));
        _logger.LogInformation("Cached video data for URL: {VideoUrl}", videoUrl);

        return (metadata, transcript);
    }

    [Description("Extract transcript and metadata from a YouTube video URL")]
    public async Task<string> ExtractYouTubeVideo(
        [Description("The YouTube video URL to extract")] string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            throw new ArgumentException("Video URL cannot be null or empty", nameof(videoUrl));
        }

        try
        {
            var (metadata, transcript) = await GetVideoDataAsync(videoUrl);

            return $"Extracted video: {metadata.Title} by {metadata.Author}. " +
                   $"Duration: {metadata.Duration}. Transcript has {transcript.Count} segments.";
        }
        catch (Exception ex)
        {
            return $"Error extracting video: {ex.Message}";
        }
    }

    [Description("Extract and chunk a YouTube video transcript into overlapping segments for analysis")]
    public async Task<string> ChunkTranscriptForAnalysis(
        [Description("The YouTube video URL to process")] string videoUrl,
        [Description("Characters per chunk (default: 4000 ≈ 1000 tokens)")] int chunkSize = 4000,
        [Description("Overlap between chunks in characters (default: 400 ≈ 100 tokens)")] int overlapSize = 400)
    {
        try
        {
            var (metadata, transcript) = await GetVideoDataAsync(videoUrl);
            var chunks = await _chunkingService.ChunkTranscriptAsync(transcript, chunkSize, overlapSize);

            var totalChars = chunks.Sum(c => c.Text.Length);
            return $"Successfully chunked video '{metadata.Title}' (Duration: {metadata.Duration})\n" +
                   $"Generated {chunks.Count} chunks from {transcript.Count} original segments\n" +
                   $"Total characters: {totalChars}\n" +
                   $"Chunk size: {chunkSize} chars, Overlap: {overlapSize} chars\n" +
                   $"First chunk preview: {chunks[0].Text.Substring(0, Math.Min(100, chunks[0].Text.Length))}...";
        }
        catch (Exception ex)
        {
            return $"Error chunking transcript: {ex.Message}";
        }
    }

    [Description("Generate a concise 3-5 bullet point summary of a YouTube video's content")]
    public async Task<string> SummarizeVideo(
        [Description("The YouTube video URL to summarize")] string videoUrl,
        [Description("Maximum number of chunks to process (default: 10)")] int maxChunks = 10)
    {
        try
        {
            var (metadata, transcript) = await GetVideoDataAsync(videoUrl);
            var chunks = await _chunkingService.ChunkTranscriptAsync(transcript, 4000, 400);

            var chunksToProcess = chunks.Take(maxChunks).ToList();
            var combinedText = string.Join("\n\n", chunksToProcess.Select(c => c.Text));

            var prompt = $@"You are a video content summarization expert. Summarize this YouTube video transcript in exactly 3-5 concise bullet points focusing on main topics and key takeaways.

Video Title: {metadata.Title}
Video Duration: {metadata.Duration}
Channel: {metadata.Author}

Transcript (first {chunksToProcess.Count} segments):
{combinedText}

IMPORTANT: Format your response using markdown for better readability:
- Use **bold** for emphasis on key terms
- Use `code formatting` for technical terms or concepts
- Use bullet points with the format: - **Key Point**: Description with **emphasis**
- You can include sub-bullets with indentation (  - Sub-point: Details)

Example format:
- **Main Topic**: Description of the key concept with **important details**
- **Another Finding**: Explanation including `technical term` and **emphasis**
- **Conclusion**: Final takeaway with **key insight**

Provide the summary using this markdown formatting.";

            // Use orchestrator's RunAsync (it will use the LLM internally)
            var summary = await _orchestrator.RunAsync(prompt);

            return $"Summary of '{metadata.Title}' ({metadata.Duration}):\n\n{summary}\n\n" +
                   $"Note: Summary based on first {chunksToProcess.Count} of {chunks.Count} total chunks.";
        }
        catch (Exception ex)
        {
            return $"Error generating summary for video: {ex.Message}";
        }
    }

    [Description("Answer questions about a previously analyzed document using conversational context")]
    private async Task<string> AnswerQuestionAboutDocument(
        [Description("The question to answer about the document")] string question,
        [Description("The document content chunks to use as context")] string documentContext,
        [Description("The conversation history for maintaining context")] string conversationHistory)
    {
        try
        {
            var prompt = $@"You are a helpful assistant that answers questions about documents based on the provided context.

{(!string.IsNullOrEmpty(conversationHistory) ? $"Previous conversation:\n{conversationHistory}\n\n" : "")}

Document Context:
{documentContext}

Question: {question}

Please answer the question based on the document context above. Follow these guidelines:
1. Base your answer primarily on the provided document content
2. Reference specific parts of the document that support your answer
3. If the document doesn't contain information to answer the question, state that clearly
4. Maintain a conversational tone, referencing previous questions when relevant
5. Start your response with ""Based on the document..."" when appropriate
6. Include which chunk indices were most relevant to your answer

CRITICAL: You MUST respond with valid JSON only. No other text, no markdown, no explanations outside the JSON.

Required JSON format:
{{
  ""answer"": ""Your detailed answer here"",
  ""relevantChunks"": [1, 4, 7]
}}

Example valid response:
{{
  ""answer"": ""Based on the document, the main topic is artificial intelligence and its applications in healthcare. The document discusses machine learning algorithms for medical diagnosis and their accuracy rates."",
  ""relevantChunks"": [2, 5, 8]
}}";

            var agentResponse = await _orchestrator.RunAsync(prompt);
            return agentResponse.Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering question about document");
            return $"{{\"answer\": \"Error answering question: {ex.Message}\", \"relevantChunks\": []}}";
        }
    }

    /// <summary>
    /// Asks a question about a previously analyzed document using conversational context.
    /// </summary>
    /// <param name="question">The question to ask about the document</param>
    /// <param name="chunks">The document chunks to use as context</param>
    /// <param name="threadId">The thread identifier for maintaining conversation state</param>
    /// <param name="conversationHistory">The history of previous messages in the conversation</param>
    /// <returns>The answer to the question with relevant chunk references</returns>
    public async Task<string> AskQuestionAsync(
        string question,
        List<DocumentChunk> chunks,
        string threadId,
        List<ConversationMessage> conversationHistory)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be null or empty", nameof(question));
        }

        if (chunks == null || !chunks.Any())
        {
            throw new ArgumentException("Document chunks cannot be null or empty", nameof(chunks));
        }

        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new ArgumentException("Thread ID cannot be null or empty", nameof(threadId));
        }

        try
        {
            // Get or create thread from cache
            if (!_activeThreads.ContainsKey(threadId))
            {
                var agent = await _threadManagementService.GetAgentAsync(threadId);
                if (agent == null)
                {
                    _logger.LogWarning("Thread {ThreadId} not found, creating new thread", threadId);
                    // Note: In a real implementation, you might want to create a new thread here
                    // For now, we'll use the orchestrator as a fallback
                    _activeThreads[threadId] = _orchestrator;
                }
                else
                {
                    _activeThreads[threadId] = agent;
                }
            }

            // Build document context from chunks
            var documentContext = string.Join("\n\n", chunks.Select((chunk, index) =>
                $"Chunk {index + 1}:\n{chunk.Content}"));

            // Serialize conversation history for prompt
            var conversationHistoryText = conversationHistory?.Any() == true
                ? string.Join("\n", conversationHistory.Select(msg => $"{msg.Role}: {msg.Content}"))
                : string.Empty;

            // Call the AnswerQuestionAboutDocument tool
            var response = await AnswerQuestionAboutDocument(question, documentContext, conversationHistoryText);

            // Get the thread for the RunAsync call
            var thread = _activeThreads[threadId] as AIAgent ?? _orchestrator;

            // Parse JSON response and validate
            try
            {
                _logger.LogDebug("Attempting to parse AI response: {Response}", response);
                
                var jsonDoc = JsonDocument.Parse(response);
                
                // Validate required properties exist
                if (!jsonDoc.RootElement.TryGetProperty("answer", out var answerElement))
                {
                    throw new JsonException("Missing required property 'answer' in JSON response");
                }
                
                if (!jsonDoc.RootElement.TryGetProperty("relevantChunks", out var chunksElement))
                {
                    throw new JsonException("Missing required property 'relevantChunks' in JSON response");
                }

                var answer = answerElement.GetString() ?? string.Empty;
                
                var relevantChunks = new List<int>();
                if (chunksElement.ValueKind == JsonValueKind.Array)
                {
                    relevantChunks = chunksElement
                        .EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.Number)
                        .Select(x => x.GetInt32())
                        .ToList();
                }

                _logger.LogInformation("Successfully answered question for thread {ThreadId} using {ChunkCount} chunks",
                    threadId, relevantChunks.Count);

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response from AnswerQuestionAboutDocument. Response preview: {ResponsePreview}",
                    response.Substring(0, Math.Min(200, response.Length)));
                
                // Try to clean the response first
                var cleanedResponse = CleanJsonResponse(response);
                if (cleanedResponse != response)
                {
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(cleanedResponse);
                        var answer = jsonDoc.RootElement.GetProperty("answer").GetString() ?? cleanedResponse;
                        _logger.LogInformation("Successfully parsed cleaned JSON response for thread {ThreadId}", threadId);
                        return cleanedResponse;
                    }
                    catch (JsonException cleanEx)
                    {
                        _logger.LogWarning(cleanEx, "Failed to parse cleaned JSON response as well for thread {ThreadId}", threadId);
                    }
                }
                
                // Return a fallback response in the expected format
                var sanitizedResponse = response.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
                return $"{{\"answer\": \"{sanitizedResponse}\", \"relevantChunks\": []}}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AskQuestionAsync for thread {ThreadId}", threadId);
            return $"{{\"answer\": \"Error processing question: {ex.Message}\", \"relevantChunks\": []}}";
        }
    }

    public async Task<string> RunAsync(string input)
    {
        AgentRunResponse response = await _orchestrator.RunAsync(input);
        return response.Text;
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
                JsonDocument.Parse(jsonContent);
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
}