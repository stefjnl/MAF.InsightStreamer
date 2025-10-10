using MAF.InsightStreamer.Domain.Models;

namespace MAF.InsightStreamer.Application.DTOs;

/// <summary>
/// Represents the response from document analysis, containing insights and metadata.
/// </summary>
public record DocumentAnalysisResponse
{
    /// <summary>
    /// Gets or sets the summary of the document content.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of key points extracted from the document.
    /// </summary>
    public List<string> KeyPoints { get; init; } = new();

    /// <summary>
    /// Gets or sets the metadata information about the analyzed document.
    /// </summary>
    public DocumentMetadata Metadata { get; init; } = null!;

    /// <summary>
    /// Gets or sets the number of chunks the document was divided into for processing.
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Gets or sets the total processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier for the document session created for Q&A.
    /// </summary>
    public Guid SessionId { get; init; }
}