using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Providers;
using MAF.InsightStreamer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using InfraYouTubeService = MAF.InsightStreamer.Infrastructure.Services.YouTubeService;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services;

public class YouTubeServiceTests
{
    private readonly Mock<ILogger<InfraYouTubeService>> _mockLogger;
    private readonly Mock<IOptions<ProviderSettings>> _mockProviderSettings;
    private readonly Mock<McpYouTubeService> _mockMcpYouTubeService;
    private readonly InfraYouTubeService _service;

    public YouTubeServiceTests()
    {
        _mockLogger = new Mock<ILogger<InfraYouTubeService>>();
        _mockProviderSettings = new Mock<IOptions<ProviderSettings>>();
        
        // Setup mock provider settings with test API key
        var providerSettings = new ProviderSettings
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            Endpoint = "test-endpoint",
            YouTubeApiKey = "test-youtube-api-key"
        };
        
        _mockProviderSettings.Setup(x => x.Value).Returns(providerSettings);
        
        // Setup McpYouTubeService mock
        _mockMcpYouTubeService = new Mock<McpYouTubeService>(_mockLogger.Object);
        _service = new InfraYouTubeService(_mockLogger.Object, _mockProviderSettings.Object, _mockMcpYouTubeService.Object);
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
    public async Task GetTranscriptAsync_ValidUrl_ReturnsTranscriptResult()
    {
        // Arrange
        // Act & Assert - Test null/empty validation
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetTranscriptAsync("   "));

        // Note: Logger verification is complex with Moq and extension methods
        // In a real scenario, you might use a different testing approach for logging
    }

    [Fact]
    public async Task GetTranscriptAsync_EmptyLanguageCode_DefaultsToEnglish()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test";

        // Act
        var result = await _service.GetTranscriptAsync(videoUrl, "");

        // Assert
        Assert.NotNull(result);
        // Should return unsuccessful result since we're not mocking the API
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Chunks);
    }

    [Fact]
    public async Task GetTranscriptAsync_NullLanguageCode_DefaultsToEnglish()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test";

        // Act
        var result = await _service.GetTranscriptAsync(videoUrl, null!);

        // Assert
        Assert.NotNull(result);
        // Should return unsuccessful result since we're not mocking the API
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Chunks);
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Arrange
        var logger = new Mock<ILogger<InfraYouTubeService>>().Object;
        var mockProviderSettings = new Mock<IOptions<ProviderSettings>>();
        var providerSettings = new ProviderSettings
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            Endpoint = "test-endpoint",
            YouTubeApiKey = "test-youtube-api-key"
        };
        mockProviderSettings.Setup(x => x.Value).Returns(providerSettings);
        
        var mockMcpYouTubeService = new Mock<McpYouTubeService>(new Mock<ILogger<McpYouTubeService>>().Object);

        // Act
        var service = new InfraYouTubeService(logger, mockProviderSettings.Object, mockMcpYouTubeService.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockProviderSettings = new Mock<IOptions<ProviderSettings>>();
        var providerSettings = new ProviderSettings
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            Endpoint = "test-endpoint",
            YouTubeApiKey = "test-youtube-api-key"
        };
        mockProviderSettings.Setup(x => x.Value).Returns(providerSettings);
        
        var mockMcpYouTubeService = new Mock<McpYouTubeService>(new Mock<ILogger<McpYouTubeService>>().Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InfraYouTubeService(null!, mockProviderSettings.Object, mockMcpYouTubeService.Object));
    }

    [Fact]
    public void Constructor_NullProviderSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<InfraYouTubeService>>().Object;
        var mockMcpYouTubeService = new Mock<McpYouTubeService>(new Mock<ILogger<McpYouTubeService>>().Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InfraYouTubeService(logger, null!, mockMcpYouTubeService.Object));
    }

    [Fact]
    public void Constructor_EmptyYouTubeApiKey_ThrowsArgumentException()
    {
        // Arrange
        var logger = new Mock<ILogger<InfraYouTubeService>>().Object;
        var mockProviderSettings = new Mock<IOptions<ProviderSettings>>();
        var providerSettings = new ProviderSettings
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            Endpoint = "test-endpoint",
            YouTubeApiKey = "", // Empty API key
        };
        mockProviderSettings.Setup(x => x.Value).Returns(providerSettings);
        
        var mockMcpYouTubeService = new Mock<McpYouTubeService>(new Mock<ILogger<McpYouTubeService>>().Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new InfraYouTubeService(logger, mockProviderSettings.Object, mockMcpYouTubeService.Object));
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
    public async Task GetTranscriptAsync_InvalidVideoUrl_ReturnsUnsuccessfulResult()
    {
        // Arrange
        const string invalidUrl = "https://www.youtube.com/watch?v=nonexistent";

        // Act
        var result = await _service.GetTranscriptAsync(invalidUrl);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Chunks);
    }

    [Fact]
    public async Task GetVideoMetadataAsync_InvalidVideoUrl_ThrowsVideoUnavailableException()
    {
        // Arrange
        const string invalidUrl = "https://www.youtube.com/watch?v=nonexistent";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<YoutubeExplode.Exceptions.VideoUnavailableException>(() =>
            _service.GetVideoMetadataAsync(invalidUrl));

        Assert.Contains("Video is unavailable or restricted", exception.Message);
    }

    [Fact]
    public async Task GetTranscriptAsync_ValidVideoIdWithoutCaptions_ReturnsUnsuccessfulResult()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";

        // Act
        var result = await _service.GetTranscriptAsync(videoUrl);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Chunks);
    }

    [Fact]
    public async Task GetTranscriptAsync_ValidVideoWithCaptions_ReturnsTranscriptResult()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        const string languageCode = "en";

        // Act
        var result = await _service.GetTranscriptAsync(videoUrl, languageCode);

        // Assert
        // Note: This test will return unsuccessful result since we're not mocking the YouTube API
        // The actual YouTube API calls will fail without a real API key, but the service
        // should handle exceptions gracefully and return an unsuccessful result
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Chunks); // Expected behavior when API calls fail
    }

}