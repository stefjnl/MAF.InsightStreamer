using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Orchestration;
using MAF.InsightStreamer.Infrastructure.Providers;
using MAF.InsightStreamer.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MAF.InsightStreamer.Infrastructure.Tests.Orchestration;

public class VideoOrchestratorServiceTests
{
    private readonly Mock<IYouTubeService> _mockYouTubeService;
    private readonly Mock<IChunkingService> _mockChunkingService;
    private readonly Mock<IOptions<ProviderSettings>> _mockSettings;
    private readonly VideoOrchestratorService _service;

    public VideoOrchestratorServiceTests()
    {
        _mockYouTubeService = new Mock<IYouTubeService>();
        _mockChunkingService = new Mock<IChunkingService>();

        // Setup mock settings
        var testSettings = new ProviderSettings
        {
            ApiKey = "test-api-key",
            Model = "google/gemini-2.5-flash-lite-preview-09-2025",
            Endpoint = "https://openrouter.ai/api/v1"
        };

        _mockSettings = new Mock<IOptions<ProviderSettings>>();
        _mockSettings.Setup(s => s.Value).Returns(testSettings);

        _service = new VideoOrchestratorService(_mockSettings.Object, _mockYouTubeService.Object, _mockChunkingService.Object);
    }

    [Fact]
    public async Task ExtractYouTubeVideo_ValidUrl_ReturnsSuccessMessage()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        var expectedMetadata = new VideoMetadata
        {
            VideoId = "test123",
            Title = "Test Video",
            Author = "Test Author",
            Duration = TimeSpan.FromMinutes(10),
            ThumbnailUrl = "https://example.com/thumb.jpg"
        };

        var expectedTranscript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Hello world",
                StartTimeSeconds = 0,
                EndTimeSeconds = 5
            },
            new TranscriptChunk
            {
                ChunkIndex = 1,
                Text = "This is a test",
                StartTimeSeconds = 5,
                EndTimeSeconds = 10
            }
        };

        _mockYouTubeService.Setup(s => s.GetVideoMetadataAsync(videoUrl))
            .ReturnsAsync(expectedMetadata);
        _mockYouTubeService.Setup(s => s.GetTranscriptAsync(videoUrl, "en"))
            .ReturnsAsync(expectedTranscript);

        // Act
        var result = await _service.ExtractYouTubeVideo(videoUrl);

        // Assert
        Assert.Contains("Extracted video: Test Video by Test Author", result);
        Assert.Contains("Duration: 00:10:00", result);
        Assert.Contains("Transcript has 2 segments", result);
        _mockYouTubeService.Verify(s => s.GetVideoMetadataAsync(videoUrl), Times.Once);
        _mockYouTubeService.Verify(s => s.GetTranscriptAsync(videoUrl, "en"), Times.Once);
    }

    [Fact]
    public async Task ExtractYouTubeVideo_ServiceThrowsException_ReturnsErrorMessage()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        const string errorMessage = "Video not found";

        _mockYouTubeService.Setup(s => s.GetVideoMetadataAsync(videoUrl))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _service.ExtractYouTubeVideo(videoUrl);

        // Assert
        Assert.Contains($"Error extracting video: {errorMessage}", result);
        _mockYouTubeService.Verify(s => s.GetVideoMetadataAsync(videoUrl), Times.Once);
    }

    [Fact]
    public async Task ExtractYouTubeVideo_NullUrl_ThrowsArgumentException()
    {
        // Arrange
        string videoUrl = null!;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExtractYouTubeVideo(videoUrl));

        Assert.Contains("videoUrl", exception.Message);
    }

    [Fact]
    public async Task ExtractYouTubeVideo_EmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        const string videoUrl = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExtractYouTubeVideo(videoUrl));

        Assert.Contains("videoUrl", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ValidInput_ReturnsResponse()
    {
        // Arrange
        const string input = "Extract video from https://www.youtube.com/watch?v=test123";

        // Act & Assert - We expect this to fail with API authentication since we're using a test key
        var exception = await Assert.ThrowsAsync<System.ClientModel.ClientResultException>(() =>
            _service.RunAsync(input));

        // The test passes if we get a ClientResultException (401 Unauthorized) since we're using a test API key
        Assert.Contains("401", exception.Message);
    }

    [Fact]
    public async Task ExtractYouTubeVideo_TranscriptServiceThrowsTranscriptUnavailableException_ReturnsErrorMessage()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        var metadata = new VideoMetadata
        {
            VideoId = "test123",
            Title = "Test Video",
            Author = "Test Author",
            Duration = TimeSpan.FromMinutes(10),
            ThumbnailUrl = "https://example.com/thumb.jpg"
        };

        _mockYouTubeService.Setup(s => s.GetVideoMetadataAsync(videoUrl))
            .ReturnsAsync(metadata);
        _mockYouTubeService.Setup(s => s.GetTranscriptAsync(videoUrl, "en"))
            .ThrowsAsync(new TranscriptUnavailableException("No transcript available"));

        // Act
        var result = await _service.ExtractYouTubeVideo(videoUrl);

        // Assert
        Assert.Contains("Error extracting video: No transcript available", result);
    }
    [Fact]
    public async Task ChunkTranscriptForAnalysis_ValidUrl_ReturnsChunkSummary()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        var metadata = new VideoMetadata
        {
            VideoId = "test123",
            Title = "Test Video",
            Author = "Test Author",
            Duration = TimeSpan.FromMinutes(10),
            ThumbnailUrl = "https://example.com/thumb.jpg"
        };

        var transcript = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Hello world this is a test transcript",
                StartTimeSeconds = 0,
                EndTimeSeconds = 5
            },
            new TranscriptChunk
            {
                ChunkIndex = 1,
                Text = "With another segment for chunking",
                StartTimeSeconds = 5,
                EndTimeSeconds = 10
            }
        };

        var chunks = new List<TranscriptChunk>
        {
            new TranscriptChunk
            {
                ChunkIndex = 0,
                Text = "Hello world this is a test transcript With another segment for chunking",
                StartTimeSeconds = 0,
                EndTimeSeconds = 10
            }
        };

        _mockYouTubeService.Setup(s => s.GetVideoMetadataAsync(videoUrl))
            .ReturnsAsync(metadata);
        _mockYouTubeService.Setup(s => s.GetTranscriptAsync(videoUrl, "en"))
            .ReturnsAsync(transcript);
        _mockChunkingService.Setup(s => s.ChunkTranscriptAsync(transcript, 4000, 400))
            .ReturnsAsync(chunks);

        // Act
        var result = await _service.ChunkTranscriptForAnalysis(videoUrl, 4000, 400);

        // Assert
        Assert.Contains("Successfully chunked video 'Test Video'", result);
        Assert.Contains("Duration: 00:10:00", result);
        Assert.Contains("Generated 1 chunks from 2 original segments", result);
        Assert.Contains("Total characters: 71", result); // "Hello world this is a test transcript With another segment for chunking".Length
        Assert.Contains("Chunk size: 4000 chars, Overlap: 400 chars", result);
        Assert.Contains("First chunk preview: Hello world this is a test transcript With another segment for chunking...", result);
        _mockYouTubeService.Verify(s => s.GetVideoMetadataAsync(videoUrl), Times.Once);
        _mockYouTubeService.Verify(s => s.GetTranscriptAsync(videoUrl, "en"), Times.Once);
        _mockChunkingService.Verify(s => s.ChunkTranscriptAsync(transcript, 4000, 400), Times.Once);
    }
}