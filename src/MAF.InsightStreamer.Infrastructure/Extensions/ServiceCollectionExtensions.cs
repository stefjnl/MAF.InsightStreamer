using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Infrastructure.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<VideoOrchestratorService>();

        // Register other services (when implemented)
        // services.AddScoped<YouTubeService>();
        // services.AddScoped<ChunkingService>();
        // services.AddMemoryCache();
        // services.AddScoped<VideoCacheService>();

        return services;
    }
}