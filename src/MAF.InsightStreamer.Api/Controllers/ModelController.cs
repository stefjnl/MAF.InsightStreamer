using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace MAF.InsightStreamer.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelController : ControllerBase
    {
        private readonly IModelDiscoveryService _discoveryService;
        private readonly IContentOrchestratorService _orchestrator;
        private readonly IConfiguration _configuration;
        private readonly IThreadMigrationService _threadMigrationService;

        public ModelController(
            IModelDiscoveryService discoveryService,
            IContentOrchestratorService orchestrator,
            IConfiguration configuration,
            IThreadMigrationService threadMigrationService)
        {
            _discoveryService = discoveryService;
            _orchestrator = orchestrator;
            _configuration = configuration;
            _threadMigrationService = threadMigrationService;
        }

        [HttpGet("providers")]
        public IActionResult GetAvailableProviders()
        {
            return Ok(new
            {
                Providers = Enum.GetValues<ModelProvider>()
                    .Select(p => new { Name = p.ToString(), Value = (int)p }),
                Current = _configuration["CurrentProvider"]
            });
        }

        [HttpGet("discover/{provider}")]
        public async Task<IActionResult> DiscoverModels(
            ModelProvider provider,
            CancellationToken ct = default)
        {
            if (provider is ModelProvider.OpenRouter)
                return BadRequest("OpenRouter models cannot be discovered locally");

            try
            {
                var models = await _discoveryService.DiscoverModelsAsync(provider, ct);
                return Ok(models);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, $"Provider {provider} unavailable: {ex.Message}");
            }
        }

        [HttpPost("switch")]
        public async Task<IActionResult> SwitchModel([FromBody] SwitchModelRequest request)
        {
            var config = new ProviderConfiguration
            {
                Provider = request.Provider,
                Model = request.Model,
                Endpoint = GetEndpointForProvider(request.Provider),
                ApiKey = request.Provider == ModelProvider.OpenRouter
                    ? _configuration["OpenRouter:ApiKey"]
                    : null
            };

            // Reset threads before switching to prevent context loss issues
            var resetWarning = await _threadMigrationService.ResetOnModelSwitchAsync();
            
            _orchestrator.SwitchProvider(config.Provider, config.Model, config.Endpoint, config.ApiKey);
            
            return Ok(new
            {
                Message = $"Switched to {request.Provider} with model {request.Model}",
                Warning = resetWarning
            });
        }

        private string GetEndpointForProvider(ModelProvider provider)
        {
            return provider switch
            {
                ModelProvider.OpenRouter => _configuration["Providers:OpenRouter:Endpoint"]!,
                ModelProvider.Ollama => _configuration["Providers:Ollama:Endpoint"]!,
                ModelProvider.LMStudio => _configuration["Providers:LMStudio:Endpoint"]!,
                _ => throw new ArgumentException($"Unknown provider: {provider}")
            };
        }
    }
}