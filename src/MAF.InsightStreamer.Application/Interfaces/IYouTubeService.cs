namespace MAF.InsightStreamer.Application.Interfaces;

using MAF.InsightStreamer.Domain.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for YouTube video extraction services.
/// Provides methods to extract metadata and transcript information from YouTube videos.
/// </summary>
public interface IYouTubeService
{
    /// <summary>
    /// Extracts metadata information from a YouTube video URL.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL to extract metadata from.</param>
    /// <returns>A VideoMetadata object containing the video's metadata information.</returns>
    Task<VideoMetadata> GetVideoMetadataAsync(string videoUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the transcript from a YouTube video URL.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL to extract the transcript from.</param>
    /// <param name="languageCode">The language code for the transcript (default: "en").</param>
    /// <returns>A TranscriptResult object containing the transcript segments with timing information or error details.</returns>
    Task<TranscriptResult> GetTranscriptAsync(string videoUrl, string languageCode = "en", CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a transcript extraction operation.
/// </summary>
public record TranscriptResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the transcript extraction was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or sets the list of transcript chunks if the extraction was successful.
    /// </summary>
    public List<TranscriptChunk> Chunks { get; init; } = new();

    /// <summary>
    /// Gets or sets the error message if the extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}