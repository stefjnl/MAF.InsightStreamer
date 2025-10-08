using Microsoft.AspNetCore.Mvc;
using MAF.InsightStreamer.Application.Interfaces;

namespace MAF.InsightStreamer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YouTubeController : ControllerBase
{
    private readonly IVideoOrchestratorService _orchestrator;

    public YouTubeController(IVideoOrchestratorService orchestrator)
    {
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
            // Return simple response for testing
            return Ok(new { response = "Hello from YT controller" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}