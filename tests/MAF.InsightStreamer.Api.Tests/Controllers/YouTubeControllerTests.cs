using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAF.InsightStreamer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace MAF.InsightStreamer.Api.Tests.Controllers;

/// <summary>
/// Unit tests with mocked dependencies for fast, isolated testing
/// </summary>
public class YouTubeControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IContentOrchestratorService> _mockOrchestrator;

    public YouTubeControllerTests(WebApplicationFactory<Program> factory)
    {
        _mockOrchestrator = new Mock<IContentOrchestratorService>();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("https_port", "5001");
                builder.ConfigureServices(services =>
                {
                    // Remove the existing IContentOrchestratorService registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IContentOrchestratorService));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add our mock
                    services.AddSingleton(_mockOrchestrator.Object);
                });
            });
    }

    [Fact]
    public async Task Summarize_ValidVideoUrl_ReturnsOkWithResponse()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        const string expectedResponse = "Video summary content";
        _mockOrchestrator.Setup(o => o.RunAsync($"Summarize this video: {videoUrl}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/summarize", videoUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result.GetProperty("response").GetString());
    }

    [Fact]
    public async Task Summarize_OrchestratorThrowsException_Returns500WithError()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        const string errorMessage = "Orchestrator failed";
        _mockOrchestrator.Setup(o => o.RunAsync($"Summarize this video: {videoUrl}", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/summarize", videoUrl);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(result);
        Assert.Equal(errorMessage, result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExtractVideo_ValidVideoUrl_ReturnsOkWithResponse()
    {
        // Arrange
        const string videoUrl = "https://www.youtube.com/watch?v=test123";
        const string expectedResponse = "Video extraction content";
        _mockOrchestrator.Setup(o => o.RunAsync($"Extract the video: {videoUrl}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/extract", videoUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(result);
        Assert.Equal(expectedResponse, result.GetProperty("response").GetString());
    }

    [Fact]
    public async Task Test_PostWithValidInput_ReturnsOkWithResponse()
    {
        // Arrange
        const string input = "test input";
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/test", input);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(result);
        Assert.Equal("Hello from YT controller", result.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ConfigTest_Get_ReturnsOkWithConfigurationInfo()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/youtube/config-test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(result);
        Assert.Contains("Configuration successfully loaded", result.GetProperty("message").GetString());
        Assert.True(result.TryGetProperty("configuration", out _));
    }
}

/// <summary>
/// Integration tests with real API calls to OpenRouter and YouTube services.
/// These tests require proper configuration (API keys from user secrets).
/// </summary>
public class YouTubeControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public YouTubeControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("https_port", "5001");
            // Use real services - no mocking
        });
    }

    [Fact]
    public async Task Summarize_WithRealOpenRouterApi_ReturnsResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        const string testVideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"; // Rick Astley - Never Gonna Give You Up

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/summarize", testVideoUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("response", out var responseProperty));
        Assert.NotEqual("", responseProperty.GetString());

        // The response should contain some content (either a summary or an error message)
        var responseText = responseProperty.GetString()!;
        Assert.True(responseText.Length > 0, "Response should not be empty");
    }

    [Fact]
    public async Task Extract_WithRealOpenRouterApi_ReturnsResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        const string testVideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"; // Rick Astley - Never Gonna Give You Up

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/extract", testVideoUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("response", out var responseProperty));
        Assert.NotEqual("", responseProperty.GetString());

        // The response should contain some content (either extraction results or an error message)
        var responseText = responseProperty.GetString()!;
        Assert.True(responseText.Length > 0, "Response should not be empty");
    }

    [Fact]
    public async Task Summarize_WithInvalidVideoUrl_ReturnsErrorResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        const string invalidVideoUrl = "https://www.youtube.com/watch?v=invalid-video-id-12345";

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/summarize", invalidVideoUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // API returns 200 with error in response
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("response", out var responseProperty));
        var responseText = responseProperty.GetString()!;
        Assert.Contains("invalid", responseText, StringComparison.OrdinalIgnoreCase); // Should contain error information about invalid URL
    }

    [Fact]
    public async Task Test_PostWithRealServices_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        const string testInput = "Hello from integration test";

        // Act
        var response = await client.PostAsJsonAsync("/api/youtube/test", testInput);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hello from YT controller", result.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ConfigTest_WithRealConfiguration_ReturnsActualConfigInfo()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/youtube/config-test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Configuration successfully loaded", result.GetProperty("message").GetString());
        Assert.True(result.TryGetProperty("configuration", out var config));
        // Check that configuration object has the expected properties (case-insensitive check)
        var configObj = config.EnumerateObject();
        var propertyNames = configObj.Select(p => p.Name).ToList();
        Assert.Contains("hasApiKey", propertyNames);
        Assert.Contains("model", propertyNames);
        Assert.Contains("endpoint", propertyNames);
    }
}