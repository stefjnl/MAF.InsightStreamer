
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
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
    private readonly string _transcriptServiceUrl;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the YouTubeService class.
    /// </summary>
    /// <param name="logger">The logger instance for recording service operations.</param>
    /// <param name="providerSettings">The provider settings containing YouTube API key and transcript service URL.</param>
    /// <param name="httpClient">The HTTP client for making requests to the transcript service.</param>
    public YouTubeService(ILogger<YouTubeService> logger, IOptions<ProviderSettings> providerSettings, HttpClient httpClient)
    {
        _youtubeClient = new YoutubeClient();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (providerSettings?.Value == null)
        {
            throw new ArgumentNullException(nameof(providerSettings), "Provider settings cannot be null");
        }

        _youTubeApiKey = providerSettings.Value.YouTubeApiKey;
        if (string.IsNullOrWhiteSpace(_youTubeApiKey))
        {
            throw new ArgumentException("YouTube API key is required", nameof(providerSettings));
        }

        _transcriptServiceUrl = providerSettings.Value.TranscriptServiceUrl ?? "http://localhost:7279";
        _logger.LogInformation("Transcript service URL configured: {TranscriptServiceUrl}", _transcriptServiceUrl);

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
    public async Task<VideoMetadata> GetVideoMetadataAsync(string videoUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            _logger.LogError("Video URL cannot be null or empty");
            throw new ArgumentException("Video URL cannot be null or empty", nameof(videoUrl));
        }

        try
        {
            _logger.LogInformation("Extracting metadata for video URL: {VideoUrl}", videoUrl);

            var video = await _youtubeClient.Videos.GetAsync(videoUrl, cancellationToken);

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
    /// Extracts the transcript from a YouTube video URL using the Python Flask microservice.
    /// </summary>
    /// <param name="videoUrl">The YouTube video URL to extract the transcript from.</param>
    /// <param name="languageCode">The language code for the transcript (default: "en").</param>
    /// <returns>A list of TranscriptChunk objects containing the transcript segments with timing information.</returns>
    /// <exception cref="ArgumentException">Thrown when the video URL is null, empty, or invalid.</exception>
    /// <exception cref="VideoNotFoundException">Thrown when the video cannot be found or is not accessible.</exception>
    /// <exception cref="VideoUnavailableException">Thrown when the video is private, restricted, or unavailable.</exception>
    /// <exception cref="TranscriptUnavailableException">Thrown when no transcript is available for the specified language.</exception>
    public async Task<TranscriptResult> GetTranscriptAsync(string videoUrl, string languageCode = "en", CancellationToken cancellationToken = default)
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
                var errorMessage = $"Invalid YouTube URL format: {videoUrl}";
                _logger.LogError(errorMessage);
                return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
            }

            // Use the Python microservice for transcript extraction
            var transcriptResult = await GetTranscriptViaPythonService(videoId, languageCode, cancellationToken);

            _logger.LogInformation("Successfully extracted {ChunkCount} transcript chunks for video: {VideoUrl}",
                transcriptResult.Chunks.Count, videoUrl);
            return transcriptResult;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error extracting transcript for video: {videoUrl}. Error: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
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
    /// Gets transcript from the Python Flask microservice.
    /// </summary>
    /// <param name="videoId">The YouTube video ID.</param>
    /// <param name="languageCode">The language code for the transcript.</param>
    /// <returns>A list of TranscriptChunk objects containing the transcript segments with timing information.</returns>
    private async Task<TranscriptResult> GetTranscriptViaPythonService(string videoId, string languageCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUrl = $"{_transcriptServiceUrl}/transcript/{videoId}?languages={languageCode}";
            _logger.LogInformation("Requesting transcript from Python service: {RequestUrl}", requestUrl);

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"Python transcript service returned non-success status: {response.StatusCode} for video: {videoId}";
                _logger.LogWarning(errorMessage);
                return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                var errorMessage = $"Empty response from Python transcript service for video: {videoId}";
                _logger.LogWarning(errorMessage);
                return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var pythonResponse = JsonSerializer.Deserialize<PythonTranscriptResponse>(content, jsonOptions);
            
            if (pythonResponse?.Transcript == null || pythonResponse.Transcript.Count == 0)
            {
                var errorMessage = $"No transcript segments returned from Python service for video: {videoId}";
                _logger.LogWarning(errorMessage);
                return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
            }

            // Convert Python transcript format to TranscriptChunk format
            var transcriptChunks = new List<TranscriptChunk>();
            int chunkIndex = 0;

            foreach (var segment in pythonResponse.Transcript)
            {
                var chunk = new TranscriptChunk
                {
                    ChunkIndex = chunkIndex++,
                    Text = segment.Text,
                    StartTimeSeconds = segment.Start,
                    EndTimeSeconds = segment.Start + segment.Duration
                };
                transcriptChunks.Add(chunk);
            }

            _logger.LogInformation("Successfully converted {SegmentCount} transcript segments from Python service for video: {VideoId}",
                transcriptChunks.Count, videoId);
            return new TranscriptResult { Success = true, Chunks = transcriptChunks };
        }
        catch (HttpRequestException ex)
        {
            var errorMessage = $"HTTP error calling Python transcript service for video: {videoId}. Error: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
        }
        catch (JsonException ex)
        {
            var errorMessage = $"JSON deserialization error from Python transcript service for video: {videoId}. Error: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
        }
        catch (TaskCanceledException ex)
        {
            var errorMessage = $"Timeout calling Python transcript service for video: {videoId}. Error: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error calling Python transcript service for video: {videoId}. Error: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return new TranscriptResult { Success = false, ErrorMessage = errorMessage };
        }
    }

    /// <summary>
    /// Represents the response structure from the Python transcript service.
    /// </summary>
    private class PythonTranscriptResponse
    {
        public List<PythonTranscriptSegment> Transcript { get; set; } = new();
    }

    /// <summary>
    /// Represents a single transcript segment from the Python transcript service.
    /// </summary>
    private class PythonTranscriptSegment
    {
        public string Text { get; set; } = string.Empty;
        public double Start { get; set; }
        public double Duration { get; set; }
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