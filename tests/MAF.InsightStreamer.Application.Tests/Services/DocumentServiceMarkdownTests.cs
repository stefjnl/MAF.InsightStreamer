using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Application.Services;
using MAF.InsightStreamer.Domain.Enums;
using Xunit;

namespace MAF.InsightStreamer.Application.Tests.Services;

public class DocumentServiceMarkdownTests
{
    private readonly Mock<IDocumentParserService> _mockDocumentParserService;
    private readonly Mock<IContentOrchestratorService> _mockContentOrchestratorService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<IMemoryCache> _mockMemoryCache;
    private readonly DocumentService _documentService;

    public DocumentServiceMarkdownTests()
    {
        _mockDocumentParserService = new Mock<IDocumentParserService>();
        _mockContentOrchestratorService = new Mock<IContentOrchestratorService>();
        _mockLogger = new Mock<ILogger<DocumentService>>();
        _mockMemoryCache = new Mock<IMemoryCache>();

        _documentService = new DocumentService(
            _mockDocumentParserService.Object,
            _mockContentOrchestratorService.Object,
            _mockLogger.Object,
            _mockMemoryCache.Object);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_WithMarkdownInJson_ParsesCorrectly()
    {
        // Arrange
        const string fileName = "test.pdf";
        const string analysisRequest = "Summarize this document";
        var fileStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        // Mock the cache miss
        object? cachedValue = null;
        _mockMemoryCache
            .Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
            .Returns(false);
        
        // Mock the cache Set operation
        var mockCacheEntry = new Mock<ICacheEntry>();
        _mockMemoryCache
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(mockCacheEntry.Object);

        // Mock document parsing
        _mockDocumentParserService
            .Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), DocumentType.Pdf))
            .ReturnsAsync("Test document content");

        _mockDocumentParserService
            .Setup(x => x.GetPageCountAsync(It.IsAny<Stream>(), DocumentType.Pdf))
            .ReturnsAsync(5);

        // Mock orchestrator response with markdown content in JSON
        var jsonResponseWithMarkdown = @"{
            ""summary"": ""## Document Summary\n\nThis document discusses **key concepts** including:\n\n- **Important topic**: Description with details\n- **Another topic**: More information\n\nThe main conclusion is that **markdown formatting** works well within JSON."",
            ""keyPoints"": [
                ""- **Key Finding 1**: Detailed description with **emphasis** and `technical term`"",
                ""- **Key Finding 2**: Another important point with **bold text**"",
                ""- **Key Finding 3**: Final observation with `code formatting` and **highlighting**""
            ]
        }";

        _mockContentOrchestratorService
            .Setup(x => x.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(jsonResponseWithMarkdown);

        // Act
        var result = await _documentService.AnalyzeDocumentAsync(fileStream, fileName, analysisRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("## Document Summary", result.Summary);
        Assert.Contains("**key concepts**", result.Summary);
        Assert.Contains("**Important topic**", result.Summary);
        Assert.Contains("**markdown formatting**", result.Summary);

        Assert.Equal(3, result.KeyPoints.Count);
        Assert.Contains("**Key Finding 1**", result.KeyPoints[0]);
        Assert.Contains("**emphasis**", result.KeyPoints[0]);
        Assert.Contains("`technical term`", result.KeyPoints[0]);
        Assert.Contains("**bold text**", result.KeyPoints[1]);
        Assert.Contains("**highlighting**", result.KeyPoints[2]);
    }

    [Fact]
    public void ParseAnalysisResult_WithMarkdownInJson_ParsesCorrectly()
    {
        // This test verifies that the JSON parsing logic can handle markdown content
        // We'll use reflection to access the private ParseAnalysisResult method
        var methodInfo = typeof(DocumentService).GetMethod("ParseAnalysisResult", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(methodInfo);

        // Create a DocumentService instance
        var service = new DocumentService(
            _mockDocumentParserService.Object,
            _mockContentOrchestratorService.Object,
            _mockLogger.Object,
            _mockMemoryCache.Object);

        // Test JSON with markdown content
        var jsonWithMarkdown = @"{
            ""summary"": ""## Analysis Summary\n\nThe document contains **important information** about `technical concepts`:\n\n- **Main point**: Description with **emphasis**\n- **Secondary point**: More details"",
            ""keyPoints"": [
                ""- **Finding 1**: **Critical** observation with `code`"",
                ""- **Finding 2**: **Significant** result with **bold** text""
            ]
        }";

        // Act
        var result = methodInfo.Invoke(service, new object[] { jsonWithMarkdown, _mockLogger.Object });

        // Assert
        Assert.NotNull(result);
        var tupleResult = ((string Summary, List<string> KeyPoints))result;
        
        Assert.Contains("## Analysis Summary", tupleResult.Summary);
        Assert.Contains("**important information**", tupleResult.Summary);
        Assert.Contains("`technical concepts`", tupleResult.Summary);
        Assert.Contains("**Main point**", tupleResult.Summary);
        
        Assert.Equal(2, tupleResult.KeyPoints.Count);
        Assert.Contains("**Finding 1**", tupleResult.KeyPoints[0]);
        Assert.Contains("**Critical**", tupleResult.KeyPoints[0]);
        Assert.Contains("`code`", tupleResult.KeyPoints[0]);
    }
}