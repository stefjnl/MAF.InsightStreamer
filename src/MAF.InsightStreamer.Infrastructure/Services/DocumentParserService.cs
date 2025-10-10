using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Threading;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

namespace MAF.InsightStreamer.Infrastructure.Services;

/// <summary>
/// Provides document parsing functionality for various document formats including PDF, Word, Markdown, and plain text.
/// </summary>
public class DocumentParserService : IDocumentParserService
{
    private readonly ILogger<DocumentParserService> _logger;

    /// <summary>
    /// Initializes a new instance of the DocumentParserService class.
    /// </summary>
    /// <param name="logger">The logger for recording parsing operations and errors.</param>
    public DocumentParserService(ILogger<DocumentParserService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extracts text content from a document stream based on the specified document type.
    /// </summary>
    /// <param name="fileStream">The stream containing the document data.</param>
    /// <param name="documentType">The type of document to parse.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    /// <exception cref="DocumentParsingException">Thrown when document parsing fails.</exception>
    public async Task<string> ExtractTextAsync(Stream fileStream, DocumentType documentType, CancellationToken cancellationToken = default)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        try
        {
            return documentType switch
            {
                DocumentType.Pdf => await ExtractFromPdfAsync(fileStream, cancellationToken),
                DocumentType.Word => await ExtractFromWordAsync(fileStream, cancellationToken),
                DocumentType.Markdown => await ExtractFromMarkdownAsync(fileStream, cancellationToken),
                DocumentType.PlainText => await ExtractFromPlainTextAsync(fileStream, cancellationToken),
                _ => throw new DocumentParsingException(documentType, $"Unsupported document type: {documentType}")
            };
        }
        catch (DocumentParsingException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from document of type {DocumentType}", documentType);
            throw new DocumentParsingException(documentType, $"Failed to extract text from {documentType} document", ex);
        }
    }

    /// <summary>
    /// Gets the page count of a document if applicable.
    /// </summary>
    /// <param name="fileStream">The stream containing the document data.</param>
    /// <param name="documentType">The type of document to analyze.</param>
    /// <returns>A task that represents the asynchronous operation, containing the page count if applicable, null otherwise.</returns>
    /// <exception cref="DocumentParsingException">Thrown when document analysis fails.</exception>
    public async Task<int?> GetPageCountAsync(Stream fileStream, DocumentType documentType, CancellationToken cancellationToken = default)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        try
        {
            return documentType switch
            {
                DocumentType.Pdf => await GetPdfPageCountAsync(fileStream, cancellationToken),
                _ => null // Page count not applicable for other document types
            };
        }
        catch (DocumentParsingException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get page count for document of type {DocumentType}", documentType);
            throw new DocumentParsingException(documentType, $"Failed to get page count for {documentType} document", ex);
        }
    }

    /// <summary>
    /// Extracts text content from a PDF document using PdfPig library.
    /// </summary>
    /// <param name="stream">The stream containing the PDF data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Check if stream is seekable
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for PDF processing", nameof(stream));
        }

        // Store original position to restore if needed
        var originalPosition = stream.Position;
        
        try
        {
            // Validate PDF file signature
            if (!IsValidPdfFile(stream))
            {
                throw new DocumentParsingException(DocumentType.Pdf, "The provided file is not a valid PDF document. PDF files must start with '%PDF'.");
            }

            // Reset stream position for PdfPig
            stream.Position = originalPosition;
            
            using var document = PdfDocument.Open(stream);
            var text = string.Join("\n", document.GetPages().Select(p => p.Text));
            return await Task.FromResult(text);
        }
        catch (DocumentParsingException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogError(ex, "PDF document is corrupted or has invalid structure");
            throw new DocumentParsingException(DocumentType.Pdf, "The PDF document is corrupted or has an invalid structure that cannot be parsed.", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "PDF document has invalid format or structure");
            throw new DocumentParsingException(DocumentType.Pdf, "The PDF document has an invalid format or structure.", ex);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "PDF document contains invalid data");
            throw new DocumentParsingException(DocumentType.Pdf, "The PDF document contains invalid data and cannot be processed.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF document");
            throw new DocumentParsingException(DocumentType.Pdf, "Failed to extract text from PDF document", ex);
        }
        finally
        {
            // Always restore the original stream position
            try
            {
                stream.Position = originalPosition;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore stream position after PDF parsing");
            }
        }
    }

    /// <summary>
    /// Extracts text content from a Word document using DocumentFormat.OpenXml library.
    /// </summary>
    /// <param name="stream">The stream containing the Word document data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromWordAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Check if stream is seekable
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for Word document processing", nameof(stream));
        }

        // Store original position to restore if needed
        var originalPosition = stream.Position;
        
        try
        {
            using var wordDocument = WordprocessingDocument.Open(stream, false);
            var body = wordDocument.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                return string.Empty;
            }

            var text = new StringBuilder();
            foreach (var element in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                text.AppendLine(element.InnerText);
            }

            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from Word document");
            throw new DocumentParsingException(DocumentType.Word, "Failed to extract text from Word document", ex);
        }
        finally
        {
            // Always restore the original stream position
            try
            {
                stream.Position = originalPosition;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore stream position after Word document parsing");
            }
        }
    }

    /// <summary>
    /// Extracts text content from a Markdown file.
    /// </summary>
    /// <param name="stream">The stream containing the Markdown data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromMarkdownAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Check if stream is seekable
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for Markdown document processing", nameof(stream));
        }

        // Store original position to restore if needed
        var originalPosition = stream.Position;
        
        try
        {
            // Reset position to ensure we read from the beginning
            stream.Position = 0;
            
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync(cancellationToken);
            
            // For now, return the raw markdown content
            // In the future, we could strip markdown syntax if needed
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from Markdown document");
            throw new DocumentParsingException(DocumentType.Markdown, "Failed to extract text from Markdown document", ex);
        }
        finally
        {
            // Always restore the original stream position
            try
            {
                stream.Position = originalPosition;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore stream position after Markdown document parsing");
            }
        }
    }

    /// <summary>
    /// Validates if the stream contains a valid PDF file by checking the PDF signature.
    /// </summary>
    /// <param name="stream">The stream to validate.</param>
    /// <returns>True if the stream contains a valid PDF signature, false otherwise.</returns>
    private static bool IsValidPdfFile(Stream stream)
    {
        if (stream == null || stream.Length < 5)
        {
            return false;
        }

        // Store current position
        var originalPosition = stream.Position;
        
        try
        {
            // Reset to beginning to check signature
            stream.Position = 0;
            
            // Read first 5 bytes to check for PDF signature
            var header = new byte[5];
            var bytesRead = stream.Read(header, 0, 5);
            
            if (bytesRead < 5)
            {
                return false;
            }

            // Check if the file starts with "%PDF-"
            var headerString = System.Text.Encoding.ASCII.GetString(header);
            return headerString.StartsWith("%PDF-");
        }
        catch (Exception)
        {
            // If we can't read the stream, it's not a valid PDF
            return false;
        }
        finally
        {
            // Restore original position
            try
            {
                stream.Position = originalPosition;
            }
            catch
            {
                // If we can't restore position, the caller will handle it
            }
        }
    }

    /// <summary>
    /// Extracts text content from a plain text file.
    /// </summary>
    /// <param name="stream">The stream containing the plain text data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromPlainTextAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Check if stream is seekable
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for plain text document processing", nameof(stream));
        }

        // Store original position to restore if needed
        var originalPosition = stream.Position;
        
        try
        {
            // Reset position to ensure we read from the beginning
            stream.Position = 0;
            
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from plain text document");
            throw new DocumentParsingException(DocumentType.PlainText, "Failed to extract text from plain text document", ex);
        }
        finally
        {
            // Always restore the original stream position
            try
            {
                stream.Position = originalPosition;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore stream position after plain text document parsing");
            }
        }
    }

    /// <summary>
    /// Gets the page count of a PDF document using PdfPig library.
    /// </summary>
    /// <param name="stream">The stream containing the PDF data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the page count.</returns>
    private async Task<int?> GetPdfPageCountAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Check if stream is seekable
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for PDF page count processing", nameof(stream));
        }

        // Store original position to restore if needed
        var originalPosition = stream.Position;
        
        try
        {
            // Validate PDF file signature
            if (!IsValidPdfFile(stream))
            {
                throw new DocumentParsingException(DocumentType.Pdf, "The provided file is not a valid PDF document. PDF files must start with '%PDF'.");
            }

            // Reset stream position for PdfPig
            stream.Position = originalPosition;
            
            using var document = PdfDocument.Open(stream);
            return await Task.FromResult((int?)document.NumberOfPages);
        }
        catch (DocumentParsingException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogError(ex, "PDF document is corrupted or has invalid structure");
            throw new DocumentParsingException(DocumentType.Pdf, "The PDF document is corrupted or has an invalid structure that cannot be parsed.", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "PDF document has invalid format or structure");
            throw new DocumentParsingException(DocumentType.Pdf, "The PDF document has an invalid format or structure.", ex);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "PDF document contains invalid data");
            throw new DocumentParsingException(DocumentType.Pdf, "The PDF document contains invalid data and cannot be processed.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get page count from PDF document");
            throw new DocumentParsingException(DocumentType.Pdf, "Failed to get page count from PDF document", ex);
        }
        finally
        {
            // Always restore the original stream position
            try
            {
                stream.Position = originalPosition;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore stream position after PDF page count parsing");
            }
        }
    }
}