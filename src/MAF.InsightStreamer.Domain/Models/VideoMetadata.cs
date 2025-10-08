namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents metadata information extracted from a YouTube video.
/// </summary>
public class VideoMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier of the YouTube video.
    /// </summary>
    public string VideoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the YouTube video.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author/channel name of the YouTube video.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the YouTube video.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the URL of the video thumbnail image.
    /// </summary>
    public string ThumbnailUrl { get; set; } = string.Empty;
}