
namespace MAF.InsightStreamer.Infrastructure.Services;

using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly ILogger<YouTubeService> _logger;

    /// <summary>
    /// Initializes a new instance of the YouTubeService class.
    /// </summary>
    /// <param name="logger">The logger instance for recording service operations.</param>
    public YouTubeService(ILogger<YouTubeService> logger)
    {
        _youtubeClient = new YoutubeClient();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Extracts the transcript from a YouTube video URL.
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
            _logger.LogInformation("Extracting transcript for video URL: {VideoUrl}, language: {LanguageCode}", videoUrl, languageCode);

            var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(videoUrl);
            
            // Try to get the track for the specified language, but catch the exception
            ClosedCaptionTrackInfo? trackInfo = null;
            try
            {
                trackInfo = trackManifest.GetByLanguage(languageCode);
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("No transcript found for language '{LanguageCode}', trying to find any available transcript", languageCode);
            }
            
            // If not found, try to get any available track
            if (trackInfo == null)
            {
                trackInfo = trackManifest.Tracks.FirstOrDefault();
            }

            if (trackInfo == null)
            {
                _logger.LogError("No transcript available for video: {VideoUrl}", videoUrl);
                throw new TranscriptUnavailableException($"No transcript available for video: {videoUrl}");
            }

            var track = await _youtubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);
            var transcriptChunks = new List<TranscriptChunk>();

            int chunkIndex = 0;
            foreach (var caption in track.Captions)
            {
                var chunk = new TranscriptChunk
                {
                    ChunkIndex = chunkIndex++,
                    Text = caption.Text,
                    StartTimeSeconds = caption.Offset.TotalSeconds,
                    EndTimeSeconds = (caption.Offset + caption.Duration).TotalSeconds
                };

                transcriptChunks.Add(chunk);
            }

            _logger.LogInformation("Successfully extracted {ChunkCount} transcript chunks for video: {VideoUrl}", transcriptChunks.Count, videoUrl);
            return transcriptChunks;
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
        catch (Exception ex) when (!(ex is TranscriptUnavailableException))
        {
            _logger.LogError(ex, "Unexpected error extracting transcript for video: {VideoUrl}", videoUrl);
            throw;
        }
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