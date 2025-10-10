using MAF.InsightStreamer.Domain.Enums;

namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents metadata information for a document uploaded for analysis.
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Gets the name of the uploaded file.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the type of the document.
    /// </summary>
    public DocumentType FileType { get; }

    /// <summary>
    /// Gets the size of the file in bytes.
    /// </summary>
    public long FileSizeBytes { get; }

    /// <summary>
    /// Gets the timestamp when the document was uploaded.
    /// </summary>
    public DateTime UploadTimestamp { get; }

    /// <summary>
    /// Gets the number of pages in the document, if applicable.
    /// </summary>
    public int? PageCount { get; }

    /// <summary>
    /// Initializes a new instance of the DocumentMetadata class.
    /// </summary>
    /// <param name="fileName">The name of the uploaded file.</param>
    /// <param name="fileType">The type of the document.</param>
    /// <param name="fileSizeBytes">The size of the file in bytes.</param>
    /// <param name="pageCount">The number of pages in the document, if applicable.</param>
    public DocumentMetadata(string fileName, DocumentType fileType, long fileSizeBytes, int? pageCount = null)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        FileType = fileType;
        FileSizeBytes = fileSizeBytes;
        UploadTimestamp = DateTime.UtcNow;
        PageCount = pageCount;
    }
}