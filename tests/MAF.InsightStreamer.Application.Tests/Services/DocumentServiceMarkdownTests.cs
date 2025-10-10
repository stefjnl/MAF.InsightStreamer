using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Application.Services;
using MAF.InsightStreamer.Domain.Enums;
using Xunit;
using System.IO;

namespace MAF.InsightStreamer.Application.Tests.Services;

public class DocumentServiceMarkdownTests
{
    private readonly Mock<IDocumentParserService> _mockDocumentParserService;
    private readonly Mock<IContentOrchestratorService> _mockContentOrchestratorService;
    private readonly Mock<IChunkingService> _mockChunkingService;
    private readonly Mock<IDocumentSessionService> _mockDocumentSessionService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<IMemoryCache> _mockMemoryCache;
    private readonly DocumentService _documentService;

    public DocumentServiceMarkdownTests()
    {
        _mockDocumentParserService = new Mock<IDocumentParserService>();
        _mockContentOrchestratorService = new Mock<IContentOrchestratorService>();
        _mockChunkingService = new Mock<IChunkingService>();
        _mockDocumentSessionService = new Mock<IDocumentSessionService>();
        _mockLogger = new Mock<ILogger<DocumentService>>();
        _mockMemoryCache = new Mock<IMemoryCache>();

        _documentService = new DocumentService(
            _mockDocumentParserService.Object,
            _mockContentOrchestratorService.Object,
            _mockChunkingService.Object,
            _mockDocumentSessionService.Object,
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

        // Mock chunking service
        _mockChunkingService
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MAF.InsightStreamer.Domain.Models.DocumentChunk>());

        // Mock document session service
        _mockDocumentSessionService
            .Setup(x => x.CreateSessionAsync(It.IsAny<MAF.InsightStreamer.Domain.Models.DocumentAnalysisResult>(), It.IsAny<List<MAF.InsightStreamer.Domain.Models.DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MAF.InsightStreamer.Domain.Models.DocumentSession());

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
    public async Task AnalyzeDocumentAsync_WithNonSeekableStream_ThrowsArgumentException()
    {
        // Arrange
        const string fileName = "test.pdf";
        const string analysisRequest = "Summarize this document";
        var content = "test content";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        using var nonSeekableStream = new NonSeekableStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.AnalyzeDocumentAsync(nonSeekableStream, fileName, analysisRequest));

        Assert.Contains("Stream must be seekable", exception.Message);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_WithEmptyStream_ThrowsArgumentException()
    {
        // Arrange
        const string fileName = "test.pdf";
        const string analysisRequest = "Summarize this document";
        using var emptyStream = new MemoryStream();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.AnalyzeDocumentAsync(emptyStream, fileName, analysisRequest));

        Assert.Contains("File stream is empty", exception.Message);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_WithInvalidFileName_ThrowsArgumentException()
    {
        // Arrange
        const string invalidFileName = "..\\..\\malicious.txt";
        const string analysisRequest = "Summarize this document";
        var fileStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.AnalyzeDocumentAsync(fileStream, invalidFileName, analysisRequest));

        Assert.Contains("File name contains invalid characters", exception.Message);
    }

    /// <summary>
    /// Helper class that wraps a stream but makes it non-seekable for testing.
    /// </summary>
    private class NonSeekableStream : Stream
    {
        private readonly Stream _innerStream;

        public NonSeekableStream(Stream innerStream)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => throw new NotSupportedException("Stream is not seekable");
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Stream is not seekable");
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }
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
            _mockChunkingService.Object,
            _mockDocumentSessionService.Object,
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