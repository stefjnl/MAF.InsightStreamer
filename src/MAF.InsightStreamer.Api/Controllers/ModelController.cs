using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Application.Configuration;
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
        private readonly ILogger<ModelController> _logger;

        public ModelController(
            IModelDiscoveryService discoveryService,
            IContentOrchestratorService orchestrator,
            IConfiguration configuration,
            IThreadMigrationService threadMigrationService,
            ILogger<ModelController> logger)
        {
            _discoveryService = discoveryService;
            _orchestrator = orchestrator;
            _configuration = configuration;
            _threadMigrationService = threadMigrationService;
            _logger = logger;
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
            _logger.LogInformation(
                "Model switching request received: Provider={Provider}, Model={Model}",
                request.Provider, request.Model);

            var config = new ProviderConfiguration
            {
                Provider = request.Provider,
                Model = request.Model,
                Endpoint = GetEndpointForProvider(request.Provider),
                ApiKey = request.Provider == ModelProvider.OpenRouter
                    ? _configuration["OpenRouter:ApiKey"]
                    : null
            };

            _logger.LogInformation(
                "Preparing configuration for {Provider} with model {Model} at endpoint {Endpoint}",
                config.Provider, config.Model, config.Endpoint);

            // Reset threads before switching to prevent context loss issues
            var resetWarning = await _threadMigrationService.ResetOnModelSwitchAsync();
            
            _logger.LogInformation(
                "Switching orchestrator provider to {Provider}:{Model}, thread reset warning: {Warning}",
                config.Provider, config.Model, resetWarning ?? "None");
            
            _orchestrator.SwitchProvider(config.Provider, config.Model, config.Endpoint, config.ApiKey);
            
            var response = new
            {
                Message = $"Switched to {request.Provider} with model {request.Model}",
                Warning = resetWarning
            };

            _logger.LogInformation("Successfully switched to {Provider}:{Model}", request.Provider, request.Model);
            
            return Ok(response);
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