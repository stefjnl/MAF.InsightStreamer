using MAF.InsightStreamer.Domain.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace MAF.InsightStreamer.Application.Interfaces;

/// <summary>
/// Defines the contract for document parsing services that extract text content from various document formats.
/// </summary>
public interface IDocumentParserService
{
    /// <summary>
    /// Extracts text content from a document stream.
    /// </summary>
    /// <param name="fileStream">The stream containing the document data.</param>
    /// <param name="documentType">The type of document to parse.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation, containing the extracted text content.</returns>
    /// <exception cref="MAF.InsightStreamer.Domain.Models.DocumentParsingException">Thrown when document parsing fails.</exception>
    Task<string> ExtractTextAsync(Stream fileStream, DocumentType documentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the page count of a document if applicable.
    /// </summary>
    /// <param name="fileStream">The stream containing the document data.</param>
    /// <param name="documentType">The type of document to analyze.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation, containing the page count if applicable, null otherwise.</returns>
    /// <exception cref="MAF.InsightStreamer.Domain.Models.DocumentParsingException">Thrown when document analysis fails.</exception>
    Task<int?> GetPageCountAsync(Stream fileStream, DocumentType documentType, CancellationToken cancellationToken = default);
}