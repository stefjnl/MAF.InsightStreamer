namespace MAF.InsightStreamer.Infrastructure.Services;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using MAF.InsightStreamer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

public class McpYouTubeService : IDisposable
{
    private readonly ILogger<McpYouTubeService> _logger;
    private readonly McpGatewayHostedService _gatewayService;
    private McpClient? _mcpClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    public McpYouTubeService(
        ILogger<McpYouTubeService> logger,
        McpGatewayHostedService gatewayService)
    {
        _logger = logger;
        _gatewayService = gatewayService;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            // Wait for gateway to be ready
            var maxWait = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;
            
            while (!_gatewayService.IsReady && DateTime.UtcNow - startTime < maxWait)
            {
                _logger.LogDebug("Waiting for MCP Gateway to be ready...");
                await Task.Delay(500, cancellationToken);
            }

            if (!_gatewayService.IsReady)
            {
                throw new InvalidOperationException("MCP Gateway failed to start within timeout period");
            }

            _logger.LogInformation("Connecting to MCP Gateway on port {Port}...", _gatewayService.GatewayPort);

            // Use HTTP transport to connect to streaming gateway
            var options = new HttpClientTransportOptions
            {
                Endpoint = new Uri($"http://localhost:{_gatewayService.GatewayPort}/mcp"),
                Name = "MCP Gateway HTTP Transport"
            };

            var transport = new HttpClientTransport(options);
            _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);

            _isInitialized = true;
            _logger.LogInformation("Successfully connected to MCP Gateway");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MCP client");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<TranscriptChunk>> GetTranscriptAsync(
        string videoId,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            if (_mcpClient == null)
            {
                _logger.LogError("MCP client is not initialized");
                return new List<TranscriptChunk>();
            }

            var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            _logger.LogInformation("Requesting transcript via MCP for video: {VideoId}", videoId);

            // Call the get_timed_transcript tool (includes timestamps)
            var toolArguments = new Dictionary<string, object?>
            {
                { "url", videoUrl },
                { "lang", language }
            };

            var toolResult = await _mcpClient.CallToolAsync(
                "get_timed_transcript",
                toolArguments,
                cancellationToken: cancellationToken
            );

            if (toolResult?.Content == null || !toolResult.Content.Any())
            {
                _logger.LogWarning("No transcript content returned from MCP for video: {VideoId}", videoId);
                return new List<TranscriptChunk>();
            }

            // Parse the result
            var chunks = ParseMcpTranscriptResult((IReadOnlyList<ModelContextProtocol.Protocol.ContentBlock>)toolResult.Content);

            _logger.LogInformation("Successfully retrieved {ChunkCount} transcript chunks via MCP for video: {VideoId}",
                chunks.Count, videoId);

            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transcript via MCP for video: {VideoId}", videoId);
            return new List<TranscriptChunk>();
        }
    }

    private List<TranscriptChunk> ParseMcpTranscriptResult(IReadOnlyList<ModelContextProtocol.Protocol.ContentBlock> content)
    {
        var chunks = new List<TranscriptChunk>();

        try
        {
            foreach (var item in content)
            {
                if (item is ModelContextProtocol.Protocol.TextContentBlock textBlock)
                {
                    var text = textBlock.Text;
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    // Parse the actual MCP response format
                    var response = JsonSerializer.Deserialize<McpTranscriptResponse>(text);
                    if (response?.Snippets != null)
                    {
                        int index = 0;
                        foreach (var snippet in response.Snippets)
                        {
                            chunks.Add(new TranscriptChunk
                            {
                                ChunkIndex = index++,
                                Text = snippet.Text ?? "",
                                StartTimeSeconds = snippet.Start ?? 0,
                                EndTimeSeconds = (snippet.Start ?? 0) + (snippet.Duration ?? 5)
                            });
                        }
                        return chunks;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MCP transcript result");
        }

        return chunks;
    }
public void Dispose()
{
    _initLock?.Dispose();
}

// Helper classes for JSON deserialization (actual MCP response)
private class McpTranscriptResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("snippets")]
    public List<SnippetItem>? Snippets { get; set; }
}

private class SnippetItem
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("start")]
    public double? Start { get; set; }
    
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}
}
