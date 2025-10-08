using MAF.InsightStreamer.Infrastructure.Orchestration;
using MAF.InsightStreamer.Infrastructure.Services;
using MAF.InsightStreamer.Infrastructure.Providers;
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
        services.Configure<ProviderSettings>(
            configuration.GetSection(ProviderSettings.SectionName));

        // Register YouTube service as singleton to match orchestrator lifetime
        services.AddSingleton<IYouTubeService, YouTubeService>();

        // Register orchestrator as singleton because agent is stateful
        services.AddSingleton<IVideoOrchestratorService>(sp => {
            var settings = sp.GetRequiredService<IOptions<ProviderSettings>>().Value;
            var youtubeService = sp.GetRequiredService<IYouTubeService>();
            return new VideoOrchestratorService(settings.ApiKey, settings.Model, settings.Endpoint, youtubeService);
        });

        // Register other services (when implemented)
        // services.AddScoped<ChunkingService>();
        // services.AddMemoryCache();
        // services.AddScoped<VideoCacheService>();

        return services;
    }
}