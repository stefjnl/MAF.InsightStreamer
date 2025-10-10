namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents a chunk of document text with position information for analysis.
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Gets or sets the text content of this document chunk.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sequential index of this chunk in the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the starting position of this chunk in the original document.
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// Gets or sets the ending position of this chunk in the original document.
    /// </summary>
    public int EndPosition { get; set; }
}