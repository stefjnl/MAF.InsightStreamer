using MAF.InsightStreamer.Infrastructure.Orchestration;
using MAF.InsightStreamer.Infrastructure.Services;
using MAF.InsightStreamer.Infrastructure.Providers;
using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Application.Configuration;
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
            configuration.GetSection("ProviderSettings"));
        
        // Register document processing configuration
        services.Configure<DocumentProcessingSettings>(
            configuration.GetSection("DocumentProcessing"));

        // Register question answer configuration
        services.Configure<QuestionAnswerSettings>(
            configuration.GetSection("QuestionAnswer"));

        // Register model discovery configuration
        services.Configure<ModelDiscoverySettings>(configuration.GetSection("ModelDiscoverySettings"));

        // Register MCP service as Singleton (maintains connection)
        services.AddSingleton<McpYouTubeService>();

        // Register YouTubeService with proper lifetime
        services.AddScoped<IYouTubeService, YouTubeService>();

        // Register services with proper lifetimes
        services.AddScoped<IChunkingService, ChunkingService>();
        
        // Document processing services
        services.AddScoped<IDocumentParserService, DocumentParserService>();
        services.AddScoped<IDocumentService, DocumentService>();

        // Model discovery service
        services.AddScoped<IModelDiscoveryService, ModelDiscoveryService>();

        // Register orchestrator as scoped - depends on scoped services
        services.AddScoped<IContentOrchestratorService, ContentOrchestratorService>();

        // Register chat client factory
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();

        // Register memory cache for caching functionality
        services.AddMemoryCache();
        
        // Register thread management service
        services.AddScoped<IThreadManagementService, ThreadManagementService>();
        
        // Register thread migration service
        services.AddScoped<IThreadMigrationService, ThreadMigrationService>();
        
        // Register document session service
        services.AddScoped<IDocumentSessionService, DocumentSessionService>();
        
        // Register question answer service
        services.AddScoped<IQuestionAnswerService, QuestionAnswerService>();
        
        // Register other services (when implemented)
        // services.AddScoped<IVideoCacheService, VideoCacheService>();

        return services;
    }
}