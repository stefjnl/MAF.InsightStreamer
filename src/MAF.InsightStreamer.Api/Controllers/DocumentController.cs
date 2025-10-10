using Microsoft.AspNetCore.Mvc;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Exceptions;

namespace MAF.InsightStreamer.Api.Controllers;

/// <summary>
/// API controller for document analysis operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IQuestionAnswerService _questionAnswerService;
    private readonly ILogger<DocumentController> _logger;

    /// <summary>
    /// Initializes a new instance of the DocumentController class.
    /// </summary>
    /// <param name="documentService">The document analysis service.</param>
    /// <param name="questionAnswerService">The question answering service for conversational document queries.</param>
    /// <param name="logger">The logger for the controller.</param>
    public DocumentController(
        IDocumentService documentService,
        IQuestionAnswerService questionAnswerService,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _questionAnswerService = questionAnswerService ?? throw new ArgumentNullException(nameof(questionAnswerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes an uploaded document and provides AI-powered insights.
    /// </summary>
    /// <param name="file">The document file to analyze.</param>
    /// <param name="analysisRequest">Optional custom analysis request (defaults to "Provide a concise summary").</param>
    /// <returns>The document analysis response containing summary and key points.</returns>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AnalyzeDocument(
        [FromForm] IFormFile file,
        [FromForm] string? analysisRequest = "Provide a concise summary")
    {
        try
        {
            // Validate file existence
            if (file == null)
            {
                _logger.LogWarning("Document analysis attempted without a file");
                return BadRequest("No file provided. Please upload a document file.");
            }

            // Validate file size (10MB limit)
            const long maxFileSizeBytes = 10 * 1024 * 1024; // 10MB
            if (file.Length == 0)
            {
                _logger.LogWarning("Document analysis attempted with empty file: {FileName}", file.FileName);
                return BadRequest("The uploaded file is empty. Please provide a valid document file.");
            }

            if (file.Length > maxFileSizeBytes)
            {
                _logger.LogWarning("Document analysis attempted with file exceeding size limit: {FileName}, Size: {FileSize} bytes", 
                    file.FileName, file.Length);
                return StatusCode(StatusCodes.Status413PayloadTooLarge, 
                    $"File size exceeds the maximum allowed limit of {maxFileSizeBytes / (1024 * 1024)}MB.");
            }

            // Validate file extension
            var allowedExtensions = new[] { ".pdf", ".docx", ".md", ".txt" };
            var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                _logger.LogWarning("Document analysis attempted with unsupported file type: {FileName}", file.FileName);
                return BadRequest($"Unsupported file type '{fileExtension}'. Supported file types are: {string.Join(", ", allowedExtensions)}");
            }

            // Process the document
            _logger.LogInformation("Starting document analysis for file: {FileName}, Size: {FileSize} bytes", 
                file.FileName, file.Length);

            using var fileStream = file.OpenReadStream();
            var response = await _documentService.AnalyzeDocumentAsync(fileStream, file.FileName, analysisRequest!);

            _logger.LogInformation("Document analysis completed successfully for file: {FileName}", file.FileName);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument during document analysis for file: {FileName}", file?.FileName);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during document analysis for file: {FileName}", file?.FileName);
            return UnprocessableEntity(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document analysis for file: {FileName}", file?.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An unexpected error occurred while processing the document. Please try again later.");
        }
    }

    /// <summary>
    /// Returns the list of supported document file types.
    /// </summary>
    /// <returns>An array of supported file extensions.</returns>
    [HttpGet("supported-types")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public IActionResult GetSupportedTypes()
    {
        var supportedTypes = new[] { "pdf", "docx", "md", "txt" };
        return Ok(supportedTypes);
    }

    /// <summary>
    /// Asks a question about a previously analyzed document in a conversational context.
    /// </summary>
    /// <param name="request">The question request containing session ID, question, and optional thread ID.</param>
    /// <returns>The answer to the question along with conversation context.</returns>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(QuestionAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AskQuestion([FromBody] AskQuestionRequest request)
    {
        try
        {
            // Validate request
            if (request == null)
            {
                _logger.LogWarning("Ask question attempted with null request");
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Question))
            {
                _logger.LogWarning("Ask question attempted with empty question for session: {SessionId}", request.SessionId);
                return BadRequest("Question cannot be empty.");
            }

            if (request.SessionId == Guid.Empty)
            {
                _logger.LogWarning("Ask question attempted with invalid session ID: {SessionId}", request.SessionId);
                return BadRequest("Session ID is required.");
            }

            _logger.LogInformation("Processing question for session: {SessionId}, ThreadId: {ThreadId}",
                request.SessionId, request.ThreadId);

            // Call the question answering service
            var result = await _questionAnswerService.AskQuestionAsync(
                request.SessionId,
                request.Question,
                request.ThreadId);

            // Map the result to the response DTO
            var response = new QuestionAnswerResponse
            {
                Answer = result.Answer,
                RelevantChunkIndices = result.RelevantChunkIndices,
                ThreadId = result.ThreadId,
                ConversationHistory = result.ConversationHistory,
                TotalQuestionsAsked = result.ConversationHistory.Count(m => m.Role == MessageRole.User)
            };

            _logger.LogInformation("Successfully processed question for session: {SessionId}, ThreadId: {ThreadId}",
                request.SessionId, response.ThreadId);

            return Ok(response);
        }
        catch (SessionNotFoundException ex)
        {
            _logger.LogWarning(ex, "Session not found for ID: {SessionId}", request?.SessionId);
            return NotFound("Document session not found or expired");
        }
        catch (SessionExpiredException ex)
        {
            _logger.LogWarning(ex, "Session expired for ID: {SessionId}", request?.SessionId);
            return StatusCode(StatusCodes.Status410Gone, "Document session has expired");
        }
        catch (ThreadIdMismatchException ex)
        {
            _logger.LogWarning(ex, "Thread ID mismatch for session: {SessionId}, ThreadId: {ThreadId}",
                request?.SessionId, request?.ThreadId);
            return BadRequest("ThreadId does not match document session");
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogWarning(ex, "Rate limit exceeded for session: {SessionId}", request?.SessionId);
            return StatusCode(StatusCodes.Status429TooManyRequests, "Maximum questions per session exceeded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing question for session: {SessionId}", request?.SessionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while processing the question. Please try again later.");
        }
    }
}