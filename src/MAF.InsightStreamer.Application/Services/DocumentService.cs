using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;

namespace MAF.InsightStreamer.Application.Services;

/// <summary>
/// Provides document analysis services by orchestrating document parsing and AI-powered content analysis.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IDocumentParserService _documentParserService;
    private readonly IContentOrchestratorService _contentOrchestratorService;
    private readonly ILogger<DocumentService> _logger;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the DocumentService class.
    /// </summary>
    /// <param name="documentParserService">The document parsing service.</param>
    /// <param name="contentOrchestratorService">The content orchestrator service for AI analysis.</param>
    /// <param name="logger">The logger for recording service operations.</param>
    /// <param name="memoryCache">The memory cache for storing analysis results.</param>
    public DocumentService(
        IDocumentParserService documentParserService,
        IContentOrchestratorService contentOrchestratorService,
        ILogger<DocumentService> logger,
        IMemoryCache memoryCache)
    {
        _documentParserService = documentParserService ?? throw new ArgumentNullException(nameof(documentParserService));
        _contentOrchestratorService = contentOrchestratorService ?? throw new ArgumentNullException(nameof(contentOrchestratorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <summary>
    /// Analyzes a document by extracting its content and providing AI-powered insights.
    /// </summary>
    /// <param name="fileStream">The stream containing the document data.</param>
    /// <param name="fileName">The name of the file being analyzed.</param>
    /// <param name="analysisRequest">The specific analysis request or question about the document.</param>
    /// <returns>A task that represents the asynchronous operation, containing the document analysis response.</returns>
    public async Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(Stream fileStream, string fileName, string analysisRequest)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));
        
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        
        if (string.IsNullOrWhiteSpace(analysisRequest))
            analysisRequest = "Provide a concise summary of this document";

        var startTime = DateTime.UtcNow;

        try
        {
            // Step 1: Determine DocumentType from fileName extension
            var documentType = DetermineDocumentType(fileName);
            if (documentType == DocumentType.Unknown)
            {
                throw new NotSupportedException($"File type not supported: {Path.GetExtension(fileName)}");
            }

            // Step 2: Generate cache key from file hash
            var fileHash = await ComputeFileHashAsync(fileStream);
            var cacheKey = $"document:{fileHash}";

            // Step 3: Check cache for existing analysis
            if (_memoryCache.TryGetValue(cacheKey, out DocumentAnalysisResponse? cachedResponse))
            {
                _logger.LogInformation("Returning cached analysis for file: {FileName}", fileName);
                return cachedResponse!;
            }

            // Get file size for metadata
            var fileSizeBytes = fileStream.Length;
            fileStream.Position = 0; // Reset stream position after reading length

            // Step 4: Extract text and get page count
            var extractedText = await _documentParserService.ExtractTextAsync(fileStream, documentType);
            var pageCount = await _documentParserService.GetPageCountAsync(fileStream, documentType);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("No text could be extracted from the document.");
            }

            // Step 5: Call orchestrator for AI analysis
            var orchestratorInput = BuildOrchestratorInput(fileName, extractedText, analysisRequest);
            var analysisResult = await _contentOrchestratorService.RunAsync(orchestratorInput);

            // Parse the analysis result
            var analysisData = ParseAnalysisResult(analysisResult);

            // Step 6: Build DocumentAnalysisResponse with metadata
            var metadata = new DocumentMetadata(fileName, documentType, fileSizeBytes, pageCount);
            var response = new DocumentAnalysisResponse
            {
                Summary = analysisData.Summary,
                KeyPoints = analysisData.KeyPoints,
                Metadata = metadata,
                ChunkCount = CalculateChunkCount(extractedText),
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
            };

            // Step 7: Cache result with 5-minute expiration
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            
            _memoryCache.Set(cacheKey, response, cacheEntryOptions);

            _logger.LogInformation("Successfully analyzed document: {FileName} in {ElapsedMs}ms", 
                fileName, response.ProcessingTimeMs);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Determines the document type based on the file extension.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>The document type enum value.</returns>
    private static DocumentType DetermineDocumentType(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return DocumentType.Unknown;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        return extension switch
        {
            ".pdf" => DocumentType.Pdf,
            ".docx" => DocumentType.Word,
            ".md" => DocumentType.Markdown,
            ".txt" => DocumentType.PlainText,
            _ => DocumentType.Unknown
        };
    }

    /// <summary>
    /// Computes a SHA256 hash of the file stream for cache key generation.
    /// </summary>
    /// <param name="stream">The stream to hash.</param>
    /// <returns>A task that represents the asynchronous operation, containing the base64-encoded hash.</returns>
    private static async Task<string> ComputeFileHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        stream.Position = 0; // Reset for next read
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Builds the input string for the content orchestrator.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="documentText">The extracted text content.</param>
    /// <param name="analysisRequest">The analysis request.</param>
    /// <returns>The formatted input string for the orchestrator.</returns>
    private static string BuildOrchestratorInput(string fileName, string documentText, string analysisRequest)
    {
        return $"Analyze the following document:\n" +
               $"Filename: {fileName}\n" +
               $"User Request: {analysisRequest}\n\n" +
               $"Document Content:\n{documentText}\n\n" +
               $"Provide a response in JSON format with 'summary' and 'keyPoints' fields.";
    }

    /// <summary>
    /// Parses the analysis result from the orchestrator.
    /// </summary>
    /// <param name="analysisResult">The raw analysis result string.</param>
    /// <returns>A tuple containing the summary and key points.</returns>
    private static (string Summary, List<string> KeyPoints) ParseAnalysisResult(string analysisResult)
    {
        try
        {
            using var document = JsonDocument.Parse(analysisResult);
            var root = document.RootElement;

            var summary = root.TryGetProperty("summary", out var summaryElement) 
                ? summaryElement.GetString() ?? string.Empty 
                : string.Empty;

            var keyPoints = new List<string>();
            if (root.TryGetProperty("keyPoints", out var keyPointsElement))
            {
                foreach (var element in keyPointsElement.EnumerateArray())
                {
                    var point = element.GetString();
                    if (!string.IsNullOrWhiteSpace(point))
                    {
                        keyPoints.Add(point);
                    }
                }
            }

            return (summary, keyPoints);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return the raw result as summary
            return (analysisResult, new List<string>());
        }
    }

    /// <summary>
    /// Calculates the approximate number of chunks based on text length.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>The estimated chunk count.</returns>
    private static int CalculateChunkCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Using standard chunk size of 4000 characters with 400 overlap
        const int chunkSize = 4000;
        const int overlap = 400;
        const int effectiveChunkSize = chunkSize - overlap;

        return Math.Max(1, (int)Math.Ceiling((double)(text.Length - overlap) / effectiveChunkSize));
    }
}