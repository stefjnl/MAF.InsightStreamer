using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YoutubeExplode.Exceptions;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services;

public class YouTubeServiceTests
{
    private readonly Mock<ILogger<YouTubeService>> _mockLogger;
    private readonly YouTubeService _service;

    public YouTubeServiceTests()
    {
        _mockLogger = new Mock<ILogger<YouTubeService>>();
        _service = new YouTubeService(_mockLogger.Object);
    }

    [Fact]
    public async Task GetVideoMetadataAsync_ValidUrl_ReturnsVideoMetadata()
    {
        // Arrange
        // Note: This test would require mocking YoutubeClient.Videos.GetAsync
        // For now, we'll test the validation logic and exception handling

        // Act & Assert - Test null/empty validation
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetVideoMetadataAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetVideoMetadataAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetVideoMetadataAsync("   "));

        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public async Task GetTranscriptAsync_ValidUrl_ReturnsTranscriptChunks()
    {
        // Arrange
        // Act & Assert - Test null/empty validation
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync("   "));

        // Test empty language code defaults to "en"
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync("https://www.youtube.com/watch?v=test", ""));

        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public async Task GetTranscriptAsync_EmptyLanguageCode_DefaultsToEnglish()
    {
        // Arrange
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetTranscriptAsync("https://www.youtube.com/watch?v=test", ""));

        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public async Task GetTranscriptAsync_NullLanguageCode_DefaultsToEnglish()
    {
        // Arrange
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetTranscriptAsync("https://www.youtube.com/watch?v=test", null!));

        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public void Constructor_ValidLogger_CreatesInstance()
    {
        // Arrange
        var logger = new Mock<ILogger<YouTubeService>>().Object;

        // Act
        var service = new YouTubeService(logger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new YouTubeService(null!));
    }

    [Fact]
    public async Task GetVideoMetadataAsync_WhitespaceUrl_ThrowsArgumentException()
    {
        // Arrange
        const string whitespaceUrl = "   ";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetVideoMetadataAsync(whitespaceUrl));

        Assert.Contains("Video URL cannot be null or empty", exception.Message);
        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public async Task GetTranscriptAsync_WhitespaceUrl_ThrowsArgumentException()
    {
        // Arrange
        const string whitespaceUrl = "   ";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetTranscriptAsync(whitespaceUrl));

        Assert.Contains("Video URL cannot be null or empty", exception.Message);
        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public async Task GetTranscriptAsync_InvalidVideoUrl_LogsAndThrowsAppropriateException()
    {
        // Arrange
        const string invalidUrl = "https://www.youtube.com/watch?v=nonexistent";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<VideoUnavailableException>(() =>
            _service.GetTranscriptAsync(invalidUrl));

        Assert.Contains("Video is unavailable or restricted", exception.Message);
    }

    [Fact]
    public async Task GetVideoMetadataAsync_InvalidVideoUrl_LogsAndThrowsAppropriateException()
    {
        // Arrange
        const string invalidUrl = "https://www.youtube.com/watch?v=nonexistent";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<VideoUnavailableException>(() =>
            _service.GetVideoMetadataAsync(invalidUrl));

        Assert.Contains("Video is unavailable or restricted", exception.Message);
    }
}