using MAF.InsightStreamer.Infrastructure.Orchestration;
using MAF.InsightStreamer.Infrastructure.Services;
using MAF.InsightStreamer.Infrastructure.Providers;
using MAF.InsightStreamer.Application.Interfaces;
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
        services.Configure<ProviderSettings>(
            configuration.GetSection(ProviderSettings.SectionName));

        // Register services with proper lifetimes
        services.AddScoped<IYouTubeService, YouTubeService>();
        services.AddScoped<IChunkingService, ChunkingService>();

        // Register orchestrator as scoped - depends on scoped services
        services.AddScoped<IVideoOrchestratorService, VideoOrchestratorService>();

        // Register other services (when implemented)
        // services.AddMemoryCache();
        // services.AddScoped<IVideoCacheService, VideoCacheService>();

        return services;
    }
}