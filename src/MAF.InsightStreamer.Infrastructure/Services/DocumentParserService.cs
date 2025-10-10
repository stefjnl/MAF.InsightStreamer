using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text;
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
    public async Task<string> ExtractTextAsync(Stream fileStream, DocumentType documentType)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        try
        {
            return documentType switch
            {
                DocumentType.Pdf => await ExtractFromPdfAsync(fileStream),
                DocumentType.Word => await ExtractFromWordAsync(fileStream),
                DocumentType.Markdown => await ExtractFromMarkdownAsync(fileStream),
                DocumentType.PlainText => await ExtractFromPlainTextAsync(fileStream),
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
    public async Task<int?> GetPageCountAsync(Stream fileStream, DocumentType documentType)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        try
        {
            return documentType switch
            {
                DocumentType.Pdf => await GetPdfPageCountAsync(fileStream),
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
    private async Task<string> ExtractFromPdfAsync(Stream stream)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            var text = string.Join("\n", document.GetPages().Select(p => p.Text));
            return await Task.FromResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF document");
            throw new DocumentParsingException(DocumentType.Pdf, "Failed to extract text from PDF document", ex);
        }
    }

    /// <summary>
    /// Extracts text content from a Word document using DocumentFormat.OpenXml library.
    /// </summary>
    /// <param name="stream">The stream containing the Word document data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromWordAsync(Stream stream)
    {
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
    }

    /// <summary>
    /// Extracts text content from a Markdown file.
    /// </summary>
    /// <param name="stream">The stream containing the Markdown data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromMarkdownAsync(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            
            // For now, return the raw markdown content
            // In the future, we could strip markdown syntax if needed
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from Markdown document");
            throw new DocumentParsingException(DocumentType.Markdown, "Failed to extract text from Markdown document", ex);
        }
    }

    /// <summary>
    /// Extracts text content from a plain text file.
    /// </summary>
    /// <param name="stream">The stream containing the plain text data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    private async Task<string> ExtractFromPlainTextAsync(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from plain text document");
            throw new DocumentParsingException(DocumentType.PlainText, "Failed to extract text from plain text document", ex);
        }
    }

    /// <summary>
    /// Gets the page count of a PDF document using PdfPig library.
    /// </summary>
    /// <param name="stream">The stream containing the PDF data.</param>
    /// <returns>A task that represents the asynchronous operation, containing the page count.</returns>
    private async Task<int?> GetPdfPageCountAsync(Stream stream)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            return await Task.FromResult((int?)document.NumberOfPages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get page count from PDF document");
            throw new DocumentParsingException(DocumentType.Pdf, "Failed to get page count from PDF document", ex);
        }
    }
}