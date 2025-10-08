using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MAF.InsightStreamer.Infrastructure.Providers;

namespace MAF.InsightStreamer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigTestController : ControllerBase
{
    private readonly ProviderSettings _providerSettings;

    public ConfigTestController(IOptions<ProviderSettings> providerSettings)
    {
        _providerSettings = providerSettings.Value;
    }

    /// <summary>
    /// Test endpoint to verify configuration binding from user secrets
    /// </summary>
    [HttpGet("test")]
    public IActionResult TestConfiguration()
    {
        try
        {
            // Test that configuration is properly bound and reading from user secrets
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
}