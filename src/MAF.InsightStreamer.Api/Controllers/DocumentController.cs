using Microsoft.AspNetCore.Mvc;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;

namespace MAF.InsightStreamer.Api.Controllers;

/// <summary>
/// API controller for document analysis operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentController> _logger;

    /// <summary>
    /// Initializes a new instance of the DocumentController class.
    /// </summary>
    /// <param name="documentService">The document analysis service.</param>
    /// <param name="logger">The logger for the controller.</param>
    public DocumentController(
        IDocumentService documentService,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
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
}