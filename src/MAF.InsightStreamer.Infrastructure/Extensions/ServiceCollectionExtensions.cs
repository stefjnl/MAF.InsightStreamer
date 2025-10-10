using MAF.InsightStreamer.Infrastructure.Orchestration;
using MAF.InsightStreamer.Infrastructure.Services;
using MAF.InsightStreamer.Infrastructure.Providers;
using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Application.Services;
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
        
        // Register document processing configuration
        services.Configure<DocumentProcessingSettings>(
            configuration.GetSection("DocumentProcessing"));

        // Register HttpClient for YouTubeService
        services.AddHttpClient<IYouTubeService, YouTubeService>();

        // Register services with proper lifetimes
        services.AddScoped<IChunkingService, ChunkingService>();
        
        // Document processing services
        services.AddScoped<IDocumentParserService, DocumentParserService>();
        services.AddScoped<IDocumentService, DocumentService>();

        // Register orchestrator as scoped - depends on scoped services
        services.AddScoped<IContentOrchestratorService, ContentOrchestratorService>();

        // Register other services (when implemented)
        // services.AddMemoryCache();
        // services.AddScoped<IVideoCacheService, VideoCacheService>();

        return services;
    }
}