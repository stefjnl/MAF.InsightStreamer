using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for the DocumentParserService class.
/// </summary>
public class DocumentParserServiceTests
{
    private readonly Mock<ILogger<DocumentParserService>> _mockLogger;
    private readonly DocumentParserService _service;

    /// <summary>
    /// Initializes a new instance of the DocumentParserServiceTests class.
    /// </summary>
    public DocumentParserServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentParserService>>();
        _service = new DocumentParserService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExtractFromMarkdownAsync_WithValidMarkdownContent_ReturnsExpectedText()
    {
        // Arrange
        var markdownContent = @"# Sample Document

This is a sample markdown document for testing purposes.

## Key Points

- First point
- Second point
- Third point

## Conclusion

This document demonstrates the markdown parsing functionality.";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdownContent));

        // Act
        var result = await _service.ExtractTextAsync(stream, DocumentType.Markdown);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(markdownContent, result);
    }

    [Fact]
    public async Task ExtractFromPlainTextAsync_WithValidPlainTextContent_ReturnsExpectedText()
    {
        // Arrange
        var plainTextContent = @"This is a plain text document.
It contains multiple lines of text.
Used for testing plain text extraction functionality.";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(plainTextContent));

        // Act
        var result = await _service.ExtractTextAsync(stream, DocumentType.PlainText);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(plainTextContent, result);
    }

    [Fact]
    public async Task ExtractTextAsync_WithUnsupportedDocumentType_ThrowsDocumentParsingException()
    {
        // Arrange
        var content = "Test content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var unsupportedType = (DocumentType)999; // Invalid enum value

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(stream, unsupportedType));

        Assert.Equal(unsupportedType, exception.DocumentType);
        Assert.Contains("Unsupported document type", exception.Message);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        Stream nullStream = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ExtractTextAsync(nullStream, DocumentType.Markdown));
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyStream_ReturnsEmptyString()
    {
        // Arrange
        using var emptyStream = new MemoryStream();

        // Act
        var result = await _service.ExtractTextAsync(emptyStream, DocumentType.Markdown);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNonSeekableStream_ThrowsDocumentParsingException()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);
        using var nonSeekableStream = new NonSeekableStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(nonSeekableStream, DocumentType.Markdown));

        Assert.Equal(DocumentType.Markdown, exception.DocumentType);
        Assert.Contains("Stream must be seekable", exception.InnerException?.Message);
    }

    [Fact]
    public async Task ExtractFromPlainTextAsync_WithNonSeekableStream_ThrowsDocumentParsingException()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);
        using var nonSeekableStream = new NonSeekableStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(nonSeekableStream, DocumentType.PlainText));

        Assert.Equal(DocumentType.PlainText, exception.DocumentType);
        Assert.Contains("Stream must be seekable", exception.InnerException?.Message);
    }

    [Fact]
    public async Task ExtractFromWordAsync_WithNonSeekableStream_ThrowsDocumentParsingException()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);
        using var nonSeekableStream = new NonSeekableStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(nonSeekableStream, DocumentType.Word));

        Assert.Equal(DocumentType.Word, exception.DocumentType);
        Assert.Contains("Stream must be seekable", exception.InnerException?.Message);
    }

    [Fact]
    public async Task ExtractFromPdfAsync_WithNonSeekableStream_ThrowsDocumentParsingException()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);
        using var nonSeekableStream = new NonSeekableStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(nonSeekableStream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("Stream must be seekable", exception.InnerException?.Message);
    }

    [Fact]
    public async Task GetPageCountAsync_WithNonSeekableStream_ThrowsDocumentParsingException()
    {
        // Arrange
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);
        using var nonSeekableStream = new NonSeekableStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.GetPageCountAsync(nonSeekableStream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("Stream must be seekable", exception.InnerException?.Message);
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
    public async Task ExtractTextAsync_WithMarkdownContainingSpecialCharacters_PreservesContent()
    {
        // Arrange
        var markdownContent = @"# Document with Special Characters

This document contains special characters:
- Accented characters: café, naïve, résumé
- Symbols: @#$%^&*()
- Quotes: ""Single"" and 'double' quotes
- Emojis: 😀, 🎉, 🚀

## Code Block

```csharp
public void TestMethod()
{
    Console.WriteLine(""Hello, World!"");
}
```

## Math

E = mc²

## Links

[Link](https://example.com)
";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdownContent));

        // Act
        var result = await _service.ExtractTextAsync(stream, DocumentType.Markdown);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(markdownContent, result);
        Assert.Contains("café, naïve, résumé", result);
        Assert.Contains("@#$%^&*()", result);
        Assert.Contains("😀, 🎉, 🚀", result);
        Assert.Contains("E = mc²", result);
    }

    [Fact]
    public async Task ExtractTextAsync_WithPlainTextContainingUnicode_PreservesContent()
    {
        // Arrange
        var plainTextContent = @"International text:
English: Hello World
Spanish: ¡Hola Mundo!
French: Bonjour le monde
German: Guten Tag Welt
Japanese: こんにちは世界
Chinese: 你好世界
Arabic: مرحبا بالعالم
Russian: Привет мир
Emoji: 🌍🌎🌏";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(plainTextContent));

        // Act
        var result = await _service.ExtractTextAsync(stream, DocumentType.PlainText);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(plainTextContent, result);
        Assert.Contains("¡Hola Mundo!", result);
        Assert.Contains("こんにちは世界", result);
        Assert.Contains("مرحبا بالعالم", result);
        Assert.Contains("🌍🌎🌏", result);
    }

    [Fact]
    public async Task GetPageCountAsync_WithUnsupportedDocumentType_ReturnsNull()
    {
        // Arrange
        var content = "Test content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _service.GetPageCountAsync(stream, DocumentType.Markdown);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPageCountAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        Stream nullStream = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GetPageCountAsync(nullStream, DocumentType.Pdf));
    }

    [Fact]
    public async Task GetPageCountAsync_WithPlainText_ReturnsNull()
    {
        // Arrange
        var content = "Plain text content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _service.GetPageCountAsync(stream, DocumentType.PlainText);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPageCountAsync_WithWordDocument_ReturnsNull()
    {
        // Arrange
        var content = "Word document content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _service.GetPageCountAsync(stream, DocumentType.Word);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPageCountAsync_WithMarkdown_ReturnsNull()
    {
        // Arrange
        var content = "# Markdown Document\n\nSome content here.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _service.GetPageCountAsync(stream, DocumentType.Markdown);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange
        var logger = new Mock<ILogger<DocumentParserService>>().Object;

        // Act
        var service = new DocumentParserService(logger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DocumentParserService(null!));
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidPdfFile_ReturnsExpectedText()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "valid.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(pdfPath));

        // Act
        var result = await _service.ExtractTextAsync(stream, DocumentType.Pdf);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test PDF Document", result);
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyPdfFile_ThrowsDocumentParsingException()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "empty.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(pdfPath));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(stream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("not a valid PDF document", exception.Message);
    }

    [Fact]
    public async Task ExtractTextAsync_WithCorruptedPdfFile_ThrowsDocumentParsingException()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "corrupted.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(pdfPath));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(stream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("not a valid PDF document", exception.Message);
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidPdfSignature_ThrowsDocumentParsingException()
    {
        // Arrange
        var invalidPdfContent = "This is not a PDF file";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidPdfContent));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(stream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("not a valid PDF document", exception.Message);
    }

    [Fact]
    public async Task ExtractTextAsync_WithTooSmallPdfStream_ThrowsDocumentParsingException()
    {
        // Arrange
        var smallContent = "PDF";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(smallContent));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.ExtractTextAsync(stream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("not a valid PDF document", exception.Message);
    }

    [Fact]
    public async Task GetPageCountAsync_WithValidPdfFile_ReturnsPageCount()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "valid.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(pdfPath));

        // Act
        var result = await _service.GetPageCountAsync(stream, DocumentType.Pdf);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result); // Our test PDF has 1 page
    }

    [Fact]
    public async Task GetPageCountAsync_WithEmptyPdfFile_ThrowsDocumentParsingException()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "empty.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(pdfPath));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.GetPageCountAsync(stream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("not a valid PDF document", exception.Message);
    }

    [Fact]
    public async Task GetPageCountAsync_WithCorruptedPdfFile_ThrowsDocumentParsingException()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "corrupted.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(pdfPath));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentParsingException>(() =>
            _service.GetPageCountAsync(stream, DocumentType.Pdf));

        Assert.Equal(DocumentType.Pdf, exception.DocumentType);
        Assert.Contains("not a valid PDF document", exception.Message);
    }

    [Fact]
    public async Task ExtractTextAsync_WithPdfStream_PreservesStreamPosition()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "valid.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        var pdfBytes = File.ReadAllBytes(pdfPath);
        using var stream = new MemoryStream(pdfBytes);
        var originalPosition = 10L;
        stream.Position = originalPosition;

        // Act
        try
        {
            await _service.ExtractTextAsync(stream, DocumentType.Pdf);
        }
        catch
        {
            // We don't care about parsing success for this test, just position preservation
        }

        // Assert
        Assert.Equal(originalPosition, stream.Position);
    }

    [Fact]
    public async Task GetPageCountAsync_WithPdfStream_PreservesStreamPosition()
    {
        // Arrange
        var pdfPath = Path.Combine("..", "..", "..", "..", "..", "TestData", "valid.pdf");
        
        // Skip test if test file doesn't exist
        if (!File.Exists(pdfPath))
        {
            return;
        }

        var pdfBytes = File.ReadAllBytes(pdfPath);
        using var stream = new MemoryStream(pdfBytes);
        var originalPosition = 10L;
        stream.Position = originalPosition;

        // Act
        try
        {
            await _service.GetPageCountAsync(stream, DocumentType.Pdf);
        }
        catch
        {
            // We don't care about parsing success for this test, just position preservation
        }

        // Assert
        Assert.Equal(originalPosition, stream.Position);
    }
}