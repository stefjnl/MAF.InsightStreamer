using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;
using System.Threading;

namespace MAF.InsightStreamer.Application.Services;

/// <summary>
/// Provides document analysis services by orchestrating document parsing and AI-powered content analysis.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IDocumentParserService _documentParserService;
    private readonly IContentOrchestratorService _contentOrchestratorService;
    private readonly IChunkingService _chunkingService;
    private readonly IDocumentSessionService _documentSessionService;
    private readonly ILogger<DocumentService> _logger;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the DocumentService class.
    /// </summary>
    /// <param name="documentParserService">The document parsing service.</param>
    /// <param name="contentOrchestratorService">The content orchestrator service for AI analysis.</param>
    /// <param name="chunkingService">The chunking service for document text processing.</param>
    /// <param name="documentSessionService">The document session service for Q&A session management.</param>
    /// <param name="logger">The logger for recording service operations.</param>
    /// <param name="memoryCache">The memory cache for storing analysis results.</param>
    public DocumentService(
        IDocumentParserService documentParserService,
        IContentOrchestratorService contentOrchestratorService,
        IChunkingService chunkingService,
        IDocumentSessionService documentSessionService,
        ILogger<DocumentService> logger,
        IMemoryCache memoryCache)
    {
        _documentParserService = documentParserService ?? throw new ArgumentNullException(nameof(documentParserService));
        _contentOrchestratorService = contentOrchestratorService ?? throw new ArgumentNullException(nameof(contentOrchestratorService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _documentSessionService = documentSessionService ?? throw new ArgumentNullException(nameof(documentSessionService));
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
    public async Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(Stream fileStream, string fileName, string analysisRequest, CancellationToken cancellationToken = default)
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
            // Step 1: Validate and sanitize inputs
            var sanitizedFileName = SanitizeFileName(fileName);
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                throw new ArgumentException("File name contains invalid characters after sanitization.", nameof(fileName));
            }

            // Step 2: Validate stream capabilities and content
            if (!fileStream.CanRead)
            {
                throw new ArgumentException("Stream must be readable.", nameof(fileStream));
            }

            if (!fileStream.CanSeek)
            {
                throw new ArgumentException("Stream must be seekable for document processing.", nameof(fileStream));
            }

            // Check if stream is empty
            if (fileStream.Length == 0)
            {
                throw new ArgumentException("File stream is empty.", nameof(fileStream));
            }

            // Validate file size (max 50MB)
            const long maxFileSizeBytes = 50 * 1024 * 1024; // 50MB
            if (fileStream.Length > maxFileSizeBytes)
            {
                throw new ArgumentException($"File size exceeds maximum allowed size of {maxFileSizeBytes / (1024 * 1024)}MB.", nameof(fileStream));
            }

            // Step 3: Determine DocumentType from fileName extension
            var documentType = DetermineDocumentType(sanitizedFileName);
            if (documentType == DocumentType.Unknown)
            {
                throw new NotSupportedException($"File type not supported: {Path.GetExtension(sanitizedFileName)}");
            }

            // Step 4: Generate cache key from file hash
            var fileHash = await ComputeFileHashAsync(fileStream);
            var cacheKey = $"document:{fileHash}";

            // Step 5: Check cache for existing analysis
            if (_memoryCache.TryGetValue(cacheKey, out DocumentAnalysisResponse? cachedResponse))
            {
                _logger.LogInformation("Returning cached analysis for file: {FileName}", sanitizedFileName);
                return cachedResponse!;
            }

            // Get file size for metadata
            var fileSizeBytes = fileStream.Length;
            fileStream.Position = 0; // Reset stream position after reading length

            // Step 6: Extract text and get page count
            var extractedText = await _documentParserService.ExtractTextAsync(fileStream, documentType, cancellationToken);
            var pageCount = await _documentParserService.GetPageCountAsync(fileStream, documentType, cancellationToken);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("No text could be extracted from the document.");
            }

            // Step 7: Call orchestrator for AI analysis
            var orchestratorInput = BuildOrchestratorInput(sanitizedFileName, extractedText, analysisRequest);
            var analysisResult = await _contentOrchestratorService.RunAsync(orchestratorInput, cancellationToken);

            // Log the raw response for debugging
            _logger.LogInformation("Raw orchestrator response: {Response}", analysisResult);

            // Parse the JSON response from the orchestrator
            var analysisData = ParseAnalysisResult(analysisResult, _logger);

            // Step 8: Chunk the document text for Q&A processing
            var documentChunks = await _chunkingService.ChunkDocumentAsync(extractedText, cancellationToken: cancellationToken);
            _logger.LogInformation("Document chunked into {ChunkCount} chunks for Q&A processing", documentChunks.Count);

            // Step 9: Build DocumentAnalysisResponse with metadata
            var metadata = new DocumentMetadata(sanitizedFileName, documentType, fileSizeBytes, pageCount);
            var response = new DocumentAnalysisResponse
            {
                Summary = analysisData.Summary,
                KeyPoints = analysisData.KeyPoints,
                Metadata = metadata,
                ChunkCount = documentChunks.Count,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
            };

            // Step 10: Create DocumentSession after successful analysis
            try
            {
                var documentAnalysisResult = new DocumentAnalysisResult
                {
                    Summary = analysisData.Summary,
                    KeyPoints = analysisData.KeyPoints,
                    DocumentType = documentType,
                    Metadata = metadata,
                    ChunkCount = documentChunks.Count,
                    ProcessingTimeMs = response.ProcessingTimeMs
                };

                var documentSession = await _documentSessionService.CreateSessionAsync(documentAnalysisResult, documentChunks, cancellationToken);
                response = response with { SessionId = documentSession.SessionId };
                
                _logger.LogInformation("Created document session {SessionId} for document: {FileName}",
                    documentSession.SessionId, sanitizedFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create document session for document: {FileName}", sanitizedFileName);
                // Continue without session - the analysis was successful but session creation failed
                // The response will have a default empty SessionId
            }

            // Step 11: Cache result with 5-minute expiration
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            
            _memoryCache.Set(cacheKey, response, cacheEntryOptions);

            _logger.LogInformation("Successfully analyzed document: {FileName} in {ElapsedMs}ms with SessionId: {SessionId}",
                sanitizedFileName, response.ProcessingTimeMs, response.SessionId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document: {FileName}", SanitizeFileName(fileName));
            throw;
        }
    }

    /// <summary>
    /// Determines the document type based on the file extension.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>The document type enum value.</returns>
    /// <summary>
    /// Sanitizes a file name to prevent path traversal attacks and removes invalid characters.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <returns>A sanitized file name safe for processing.</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        // Remove path traversal characters and patterns
        var sanitized = fileName
            .Replace("..", string.Empty)
            .Replace("\\", string.Empty)
            .Replace("/", string.Empty)
            .Replace(":", string.Empty)
            .Replace("*", string.Empty)
            .Replace("?", string.Empty)
            .Replace("\"", string.Empty)
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace("|", string.Empty);

        // Remove leading and trailing whitespace and dots
        sanitized = sanitized.Trim().Trim('.');

        // Ensure the name is not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
            return string.Empty;

        // Limit the length to prevent extremely long names
        const int maxFileNameLength = 255;
        if (sanitized.Length > maxFileNameLength)
            sanitized = sanitized.Substring(0, maxFileNameLength);

        return sanitized;
    }

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
               $"Provide a response in JSON format with 'summary' and 'keyPoints' fields.\n" +
               $"IMPORTANT: Format the text content within the JSON fields using markdown for better readability:\n" +
               $"- Use headers (##, ###) for sections\n" +
               $"- Use **bold** for emphasis\n" +
               $"- Use bullet points (-) or numbered lists (1., 2.) for lists\n" +
               $"- Use `code formatting` for technical terms\n" +
               $"- Use blockquotes (> ) for quotes\n\n" +
               $"Example format:\n" +
               $"{{\n" +
               $"  \"summary\": \"## Document Summary\\n\\nThis document discusses **key concepts** including:\\n\\n- **Important topic**: Description\\n- **Another topic**: Details\\n\\nThe main conclusion is...\",\n" +
               $"  \"keyPoints\": [\n" +
               $"    \"- **Key Finding 1**: Detailed description with **emphasis**\",\n" +
               $"    \"- **Key Finding 2**: Another important point\",\n" +
               $"    \"- **Key Finding 3**: Final observation with `technical term`\"\n" +
               $"  ]\n" +
               $"}}";
    }

    /// <summary>
    /// Parses the JSON analysis result from the orchestrator.
    /// </summary>
    /// <param name="analysisResult">The raw JSON analysis result string.</param>
    /// <param name="logger">The logger instance for recording parsing events.</param>
    /// <returns>A tuple containing the summary and key points.</returns>
    private (string Summary, List<string> KeyPoints) ParseAnalysisResult(string analysisResult, ILogger logger)
    {
        try
        {
            // Clean up malformed JSON that starts with ""json or similar prefixes
            var cleanedResult = CleanJsonResponse(analysisResult);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var jsonResponse = JsonSerializer.Deserialize<OrchestratorAnalysisResult>(cleanedResult, options);
            
            if (jsonResponse == null)
            {
                logger.LogWarning("Failed to deserialize orchestrator response, using fallback parsing");
                return FallbackParsing(cleanedResult);
            }

            return (jsonResponse.Summary, jsonResponse.KeyPoints);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "JSON parsing failed for orchestrator response, using fallback parsing");
            return FallbackParsing(analysisResult);
        }
    }

    /// <summary>
    /// Fallback parsing method when structured JSON parsing fails.
    /// </summary>
    /// <param name="analysisResult">The raw analysis result string.</param>
    /// <returns>A tuple containing the summary and key points.</returns>
    /// <summary>
    /// Cleans up malformed JSON responses that might contain prefixes like ""json or other artifacts.
    /// </summary>
    /// <param name="response">The raw response string.</param>
    /// <returns>The cleaned JSON string.</returns>
    private static string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // Remove common malformed prefixes
        var cleaned = response.Trim();
        
        // Remove "json prefix if present (quote json)
        if (cleaned.StartsWith("\"json", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring(5).Trim(); // "json is 5 characters
        }
        
        // Remove json prefix if present
        if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring("json".Length).Trim();
        }
        
        // Remove any remaining quotes at the start/end if they're not part of valid JSON
        if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"") && cleaned.Length > 1)
        {
            // Check if this is a valid JSON string by trying to parse it
            try
            {
                var unquoted = cleaned.Substring(1, cleaned.Length - 2);
                // If the unquoted content looks like JSON, return it unquoted
                if (unquoted.TrimStart().StartsWith("{") || unquoted.TrimStart().StartsWith("["))
                {
                    return unquoted;
                }
            }
            catch
            {
                // If parsing fails, keep original
            }
        }
        
        return cleaned;
    }

    private static (string Summary, List<string> KeyPoints) FallbackParsing(string analysisResult)
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
            // If all JSON parsing fails, return the raw result as summary
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