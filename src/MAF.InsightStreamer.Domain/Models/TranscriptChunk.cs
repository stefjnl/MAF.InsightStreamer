namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents a chunk of transcript text with timing information from a YouTube video.
/// </summary>
public class TranscriptChunk
{
    /// <summary>
    /// Gets or sets the sequential index of this chunk in the transcript.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the text content of this transcript chunk.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start time of this chunk in seconds from the beginning of the video.
    /// </summary>
    public double StartTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the end time of this chunk in seconds from the beginning of the video.
    /// </summary>
    public double EndTimeSeconds { get; set; }
}