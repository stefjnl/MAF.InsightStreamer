
namespace MAF.InsightStreamer.Infrastructure.Services;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.ClosedCaptions;

/// <summary>
/// Implementation of YouTube video extraction service using the YoutubeExplode library.
/// Provides methods to extract metadata and transcript information from YouTube videos.
/// </summary>
public class YouTubeService : IYouTubeService
{
    private readonly YoutubeClient _youtubeClient;
    private readonly Google.Apis.YouTube.v3.YouTubeService _youtubeDataApi;
    private readonly ILogger<YouTubeService> _logger;
    private readonly string _youTubeApiKey;

    /// <summary>
    /// Initializes a new instance of the YouTubeService class.
    /// </summary>
    /// <param name="logger">The logger instance for recording service operations.</param>
    /// <param name="providerSettings">The provider settings containing YouTube API key.</param>
    public YouTubeService(ILogger<YouTubeService> logger, IOptions<ProviderSettings> providerSettings)
    {
        _youtubeClient = new YoutubeClient();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (providerSettings?.Value == null)
        {
            throw new ArgumentNullException(nameof(providerSettings), "Provider settings cannot be null");
        }

        _youTubeApiKey = providerSettings.Value.YouTubeApiKey;
        if (string.IsNullOrWhiteSpace(_youTubeApiKey))
        {
            throw new ArgumentException("YouTube API key is required", nameof(providerSettings));
        }

        _youtubeDataApi = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = _youTubeApiKey,
            ApplicationName = "MAF.InsightStreamer"
        });
    }

    /// <summary>
    /// Extracts metadata information from a YouTube video URL.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL to extract metadata from.</param>
    /// <returns>A VideoMetadata object containing the video's metadata information.</returns>
    /// <exception cref="ArgumentException">Thrown when the video URL is null, empty, or invalid.</exception>
    /// <exception cref="VideoNotFoundException">Thrown when the video cannot be found or is not accessible.</exception>
    /// <exception cref="VideoUnavailableException">Thrown when the video is private, restricted, or unavailable.</exception>
    public async Task<VideoMetadata> GetVideoMetadataAsync(string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            _logger.LogError("Video URL cannot be null or empty");
            throw new ArgumentException("Video URL cannot be null or empty", nameof(videoUrl));
        }

        try
        {
            _logger.LogInformation("Extracting metadata for video URL: {VideoUrl}", videoUrl);

            var video = await _youtubeClient.Videos.GetAsync(videoUrl);

            var metadata = new VideoMetadata
            {
                VideoId = video.Id.Value,
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                Duration = video.Duration ?? TimeSpan.Zero,
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url ?? string.Empty
            };

            _logger.LogInformation("Successfully extracted metadata for video: {VideoId} - {Title}", metadata.VideoId, metadata.Title);
            return metadata;
        }
        catch (VideoUnavailableException ex)
        {
            _logger.LogError(ex, "Video is unavailable or restricted: {VideoUrl}", videoUrl);
            throw new VideoUnavailableException($"Video is unavailable or restricted: {videoUrl}");
        }
        catch (YoutubeExplodeException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("404"))
        {
            _logger.LogError(ex, "Video not found: {VideoUrl}", videoUrl);
            throw new ArgumentException($"Video not found: {videoUrl}", nameof(videoUrl), ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid video URL format: {VideoUrl}", videoUrl);
            throw new ArgumentException($"Invalid video URL format: {videoUrl}", nameof(videoUrl), ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting metadata for video: {VideoUrl}", videoUrl);
            throw;
        }
    }

    /// <summary>
    /// Extracts the transcript from a YouTube video URL using YouTube Data API v3.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL to extract the transcript from.</param>
    /// <param name="languageCode">The language code for the transcript (default: "en").</param>
    /// <returns>A list of TranscriptChunk objects containing the transcript segments with timing information.</returns>
    /// <exception cref="ArgumentException">Thrown when the video URL is null, empty, or invalid.</exception>
    /// <exception cref="VideoNotFoundException">Thrown when the video cannot be found or is not accessible.</exception>
    /// <exception cref="VideoUnavailableException">Thrown when the video is private, restricted, or unavailable.</exception>
    /// <exception cref="TranscriptUnavailableException">Thrown when no transcript is available for the specified language.</exception>
    public async Task<List<TranscriptChunk>> GetTranscriptAsync(string videoUrl, string languageCode = "en")
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            _logger.LogError("Video URL cannot be null or empty");
            throw new ArgumentException("Video URL cannot be null or empty", nameof(videoUrl));
        }

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            languageCode = "en";
            _logger.LogWarning("Language code was null or empty, defaulting to 'en'");
        }

        try
        {
            _logger.LogInformation("Extracting transcript for video URL: {VideoUrl}, language: {LanguageCode}",
                videoUrl, languageCode);

            // Extract video ID from URL
            var videoId = ExtractVideoIdFromUrl(videoUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                _logger.LogError("Invalid YouTube URL format: {VideoUrl}", videoUrl);
                return new List<TranscriptChunk>();
            }

            // Get caption tracks using YouTube Data API v3
            var captionsListRequest = _youtubeDataApi.Captions.List("snippet", videoId);
            _logger.LogInformation("Requesting caption tracks for video: {VideoId}", videoId);

            var captionTracksResponse = await captionsListRequest.ExecuteAsync();

            if (captionTracksResponse?.Items == null || captionTracksResponse.Items.Count == 0)
            {
                _logger.LogWarning("No caption tracks found for video: {VideoId}. Response items count: {Count}",
                    videoId, captionTracksResponse?.Items?.Count ?? 0);
                return new List<TranscriptChunk>();
            }

            _logger.LogInformation("Found {TrackCount} caption tracks", captionTracksResponse.Items.Count);
            foreach (var track in captionTracksResponse.Items)
            {
                _logger.LogInformation("Available track: ID={TrackId}, Language={Language}, IsAutoGenerated={IsAutoGenerated}, Name={Name}",
                    track.Id,
                    track.Snippet?.Language,
                    track.Snippet?.TrackKind == "asr",
                    track.Snippet?.Name);
            }

            // Apply caption track selection logic
            var selectedTrack = SelectCaptionTrack(captionTracksResponse.Items, languageCode);
            if (selectedTrack == null)
            {
                _logger.LogWarning("No suitable caption track found for video: {VideoId}", videoId);
                return new List<TranscriptChunk>();
            }

            _logger.LogInformation("Using caption track: Language={Language}, IsAutoGenerated={IsAutoGenerated}",
                selectedTrack.Snippet?.Language,
                selectedTrack.Snippet?.TrackKind == "asr");

            // Download the caption content using the direct API approach
            _logger.LogInformation("Downloading caption content for track: {TrackId}, Format: srt", selectedTrack.Id);

            try
            {
                // Use the direct API approach with tfmt parameter
                var captionContent = await DownloadCaptionDirectly(videoId);
                _logger.LogInformation("Caption download completed. Content length: {Length}", captionContent?.Length ?? 0);

                if (string.IsNullOrWhiteSpace(captionContent))
                {
                    _logger.LogWarning("Empty caption content downloaded for track: {TrackId}", selectedTrack.Id);
                    return new List<TranscriptChunk>();
                }

                // Log first 200 characters of content for debugging
                _logger.LogDebug("Caption content preview: {Preview}",
                    captionContent.Length > 200 ? captionContent.Substring(0, 200) + "..." : captionContent);

                // Parse the SRT content into TranscriptChunk objects
                var transcriptChunks = ParseSrtContent(captionContent);

                _logger.LogInformation("Successfully extracted {ChunkCount} transcript chunks for video: {VideoUrl}",
                    transcriptChunks.Count, videoUrl);
                return transcriptChunks;
            }
            catch (Google.GoogleApiException ex)
            {
                _logger.LogError(ex, "YouTube API error downloading captions for track: {TrackId}, Status: {Status}",
                    selectedTrack.Id, ex.HttpStatusCode);
                return new List<TranscriptChunk>();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError(ex, "YouTube API quota exceeded or invalid API key for video: {VideoUrl}", videoUrl);
            return new List<TranscriptChunk>();
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Video not found: {VideoUrl}", videoUrl);
            return new List<TranscriptChunk>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting transcript for video: {VideoUrl}", videoUrl);
            return new List<TranscriptChunk>();
        }
    }

    /// <summary>
    /// Extracts video ID from YouTube URL.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL.</param>
    /// <returns>The video ID if valid, null otherwise.</returns>
    private static string? ExtractVideoIdFromUrl(string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return null;

        // Handle various YouTube URL formats
        var patterns = new[]
        {
            @"youtube\.com/watch\?v=([a-zA-Z0-9_-]{11})",
            @"youtu\.be/([a-zA-Z0-9_-]{11})",
            @"youtube\.com/embed/([a-zA-Z0-9_-]{11})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(videoUrl, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }

        // If it's already a video ID (11 characters)
        if (Regex.IsMatch(videoUrl, @"^[a-zA-Z0-9_-]{11}$"))
            return videoUrl;

        return null;
    }

    /// <summary>
    /// Selects the best caption track based on language preferences.
    /// </summary>
    /// <param name="captionTracks">List of available caption tracks.</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <returns>The best matching caption track, or null if none found.</returns>
    private Caption? SelectCaptionTrack(IList<Caption> captionTracks, string languageCode)
    {
        if (captionTracks == null || captionTracks.Count == 0)
            return null;

        Caption? selectedTrack = null;

        // STRATEGY 1: Try exact language match (manual)
        selectedTrack = captionTracks
            .FirstOrDefault(t => t.Snippet?.Language?.Equals(languageCode, StringComparison.OrdinalIgnoreCase) == true
                              && t.Snippet.TrackKind != "asr");

        // STRATEGY 2: Try exact language match (auto-generated)
        if (selectedTrack == null)
        {
            selectedTrack = captionTracks
                .FirstOrDefault(t => t.Snippet?.Language?.Equals(languageCode, StringComparison.OrdinalIgnoreCase) == true
                                  && t.Snippet.TrackKind == "asr");
        }

        // STRATEGY 3: Try language prefix match (e.g., "en-US" matches "en")
        if (selectedTrack == null)
        {
            _logger.LogWarning("No exact match for language '{LanguageCode}', trying prefix match", languageCode);
            selectedTrack = captionTracks
                .FirstOrDefault(t => t.Snippet?.Language?.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase) == true);
        }

        // STRATEGY 4: Use first auto-generated track
        if (selectedTrack == null)
        {
            _logger.LogWarning("No language match found, trying auto-generated captions");
            selectedTrack = captionTracks
                .FirstOrDefault(t => t.Snippet?.TrackKind == "asr");
        }

        // STRATEGY 5: Fallback to any available track
        if (selectedTrack == null)
        {
            _logger.LogWarning("No auto-generated captions, using first available track");
            selectedTrack = captionTracks.FirstOrDefault();
        }

        return selectedTrack;
    }

    /// <summary>
    /// Parses SRT format content into TranscriptChunk objects.
    /// </summary>
    /// <param name="srtContent">The SRT format content.</param>
    /// <returns>List of TranscriptChunk objects.</returns>
    private static List<TranscriptChunk> ParseSrtContent(string srtContent)
    {
        var chunks = new List<TranscriptChunk>();
        var lines = srtContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int chunkIndex = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip sequence numbers
            if (int.TryParse(line, out _))
            {
                // Next line should be timestamp
                if (i + 1 < lines.Length)
                {
                    var timestampLine = lines[i + 1].Trim();
                    var timestampMatch = Regex.Match(timestampLine, @"(\d{2}):(\d{2}):(\d{2}),(\d{3})\s-->\s(\d{2}):(\d{2}):(\d{2}),(\d{3})");

                    if (timestampMatch.Success)
                    {
                        var startTime = TimeSpan.ParseExact($"{timestampMatch.Groups[1]}:{timestampMatch.Groups[2]}:{timestampMatch.Groups[3]}.{timestampMatch.Groups[4]}", "hh:mm:ss.fff", null);
                        var endTime = TimeSpan.ParseExact($"{timestampMatch.Groups[5]}:{timestampMatch.Groups[6]}:{timestampMatch.Groups[7]}.{timestampMatch.Groups[8]}", "hh:mm:ss.fff", null);

                        // Collect text lines until next timestamp or end
                        var textLines = new List<string>();
                        i += 2; // Move to text content

                        while (i < lines.Length && !Regex.IsMatch(lines[i].Trim(), @"\d{2}:\d{2}:\d{2},\d{3}\s-->\s\d{2}:\d{2}:\d{2},\d{3}") && !int.TryParse(lines[i].Trim(), out _))
                        {
                            var textLine = lines[i].Trim();
                            if (!string.IsNullOrWhiteSpace(textLine))
                            {
                                textLines.Add(textLine);
                            }
                            i++;
                        }

                        if (textLines.Count > 0)
                        {
                            var chunk = new TranscriptChunk
                            {
                                ChunkIndex = chunkIndex++,
                                Text = string.Join(" ", textLines),
                                StartTimeSeconds = startTime.TotalSeconds,
                                EndTimeSeconds = endTime.TotalSeconds
                            };
                            chunks.Add(chunk);
                        }

                        i--; // Back up one line since the outer loop will increment
                    }
                }
            }
        }

        return chunks;
    }

    /// <summary>
    /// Downloads caption content directly using the YouTube Data API v3 with tfmt parameter.
    /// </summary>
    /// <param name="captionId">The ID of the caption track to download.</param>
    /// <returns>The caption content in SRT format.</returns>
    private async Task<string> DownloadCaptionDirectly(string videoId, string language = "en")
    {
        using var httpClient = new HttpClient();

        // Use YouTube's timedtext endpoint (no auth required)
        var requestUrl = $"https://www.youtube.com/api/timedtext?lang={language}&v={videoId}";

        _logger.LogInformation("Downloading captions from timedtext endpoint for video: {VideoId}", videoId);

        var response = await httpClient.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to download captions for video {VideoId}. Status: {Status}",
                videoId, response.StatusCode);
            return string.Empty;
        }

        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Empty caption content returned for video: {VideoId}", videoId);
            return string.Empty;
        }

        _logger.LogInformation("Successfully downloaded captions for video {VideoId}. Length: {Length}",
            videoId, content.Length);

        return content;
    }
}

/// <summary>
/// Exception thrown when a transcript is not available for a YouTube video.
/// </summary>
public class TranscriptUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the TranscriptUnavailableException class.
    /// </summary>
    public TranscriptUnavailableException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the TranscriptUnavailableException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TranscriptUnavailableException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the TranscriptUnavailableException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TranscriptUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}