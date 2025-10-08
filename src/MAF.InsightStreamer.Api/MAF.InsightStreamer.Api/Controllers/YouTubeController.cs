using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Infrastructure.Providers;

namespace MAF.InsightStreamer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YouTubeController : ControllerBase
{
    private readonly ProviderSettings _providerSettings;
    private readonly IVideoOrchestratorService _orchestrator;

    public YouTubeController(
        IOptions<ProviderSettings> providerSettings,
        IVideoOrchestratorService orchestrator)
    {
        _providerSettings = providerSettings.Value;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Test endpoint to verify controller is working
    /// </summary>
    [HttpPost("test")]
    public IActionResult Test([FromBody] string input)
    {
        try
        {
            return Ok(new { response = "Hello from YT controller" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to verify configuration binding from user secrets
    /// </summary>
    [HttpGet("config-test")]
    public IActionResult ConfigTest()
    {
        try
        {
            var configInfo = new
            {
                ApiKeyPrefix = _providerSettings.ApiKey?.Substring(0, Math.Min(10, _providerSettings.ApiKey?.Length ?? 0)) + "...",
                Model = _providerSettings.Model,
                Endpoint = _providerSettings.Endpoint,
                HasApiKey = !string.IsNullOrEmpty(_providerSettings.ApiKey),
                ApiKeyLength = _providerSettings.ApiKey?.Length ?? 0
            };

            return Ok(new
            {
                message = "Configuration successfully loaded from user secrets!",
                configuration = configInfo
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract video content using the video orchestrator service
    /// </summary>
    [HttpPost("extract")]
    public async Task<IActionResult> ExtractVideo([FromBody] string videoUrl)
    {
        try
        {
            var result = await _orchestrator.RunAsync($"Extract the video: {videoUrl}");
            return Ok(new { response = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}