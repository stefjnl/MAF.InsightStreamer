using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Infrastructure.Orchestration;
using MAF.InsightStreamer.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MAF.InsightStreamer.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<LlmProviderSettings>(
            configuration.GetSection(LlmProviderSettings.SectionName));

        // Register orchestrator (singleton because agent is stateful)
        services.AddSingleton<IVideoOrchestratorService>(sp => {
            var settings = sp.GetRequiredService<IOptions<LlmProviderSettings>>().Value;
            return (IVideoOrchestratorService)new VideoOrchestratorService(settings.ApiKey, settings.Model, settings.Endpoint);
        });

        // Register other services (when implemented)
        // services.AddScoped<YouTubeService>();
        // services.AddScoped<ChunkingService>();
        // services.AddMemoryCache();
        // services.AddScoped<VideoCacheService>();

        return services;
    }
}