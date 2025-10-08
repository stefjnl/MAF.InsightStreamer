namespace MAF.InsightStreamer.Application.Interfaces;

using MAF.InsightStreamer.Domain.Models;
using System.Collections.Generic;
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
    Task<VideoMetadata> GetVideoMetadataAsync(string videoUrl);

    /// <summary>
    /// Extracts the transcript from a YouTube video URL.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL to extract the transcript from.</param>
    /// <param name="languageCode">The language code for the transcript (default: "en").</param>
    /// <returns>A list of TranscriptChunk objects containing the transcript segments with timing information.</returns>
    Task<List<TranscriptChunk>> GetTranscriptAsync(string videoUrl, string languageCode = "en");
}