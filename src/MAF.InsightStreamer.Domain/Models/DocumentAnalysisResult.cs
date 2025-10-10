using MAF.InsightStreamer.Domain.Enums;

namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents the result of a document analysis operation.
/// </summary>
public class DocumentAnalysisResult
{
    /// <summary>
    /// Gets or sets the summary of the document content.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of key points extracted from the document.
    /// </summary>
    public List<string> KeyPoints { get; set; } = new();

    /// <summary>
    /// Gets or sets the type of document that was analyzed.
    /// </summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// Gets or sets the metadata of the analyzed document.
    /// </summary>
    public DocumentMetadata Metadata { get; set; } = null!;

    /// <summary>
    /// Gets or sets the total number of chunks the document was divided into for analysis.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Gets or sets the total time in milliseconds that the analysis process took.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}