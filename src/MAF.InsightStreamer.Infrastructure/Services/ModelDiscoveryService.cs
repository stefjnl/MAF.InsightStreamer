using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Infrastructure.Configuration;

namespace MAF.InsightStreamer.Infrastructure.Services;

public class ModelDiscoveryService : IModelDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptions<ModelDiscoverySettings> _settings;
    private readonly ILogger<ModelDiscoveryService> _logger;

    public ModelDiscoveryService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<ModelDiscoverySettings> settings,
        ILogger<ModelDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailableModel>> DiscoverModelsAsync(
        ModelProvider provider, 
        CancellationToken ct = default)
    {
        var cacheKey = $"models:{provider}";
        
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<AvailableModel>? cached))
            return cached!;

        var models = provider switch
        {
            ModelProvider.Ollama => await DiscoverOllamaModelsAsync(ct),
            ModelProvider.LMStudio => await DiscoverLMStudioModelsAsync(ct),
            _ => throw new NotSupportedException($"Discovery not supported for {provider}")
        };

        _cache.Set(cacheKey, models, TimeSpan.FromMinutes(_settings.Value.DiscoveryCacheMinutes));
        return models;
    }

    public async Task<bool> ValidateEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(endpoint, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<AvailableModel>> DiscoverOllamaModelsAsync(CancellationToken ct)
    {
        // GET http://localhost:11434/api/tags
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            $"{_settings.Value.OllamaEndpoint}/api/tags", ct);
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(ct);
        
        return json!.Models.Select(m => new AvailableModel(
            Id: m.Name,
            Name: m.Name,
            Provider: ModelProvider.Ollama,
            SizeBytes: m.Size,
            ModifiedAt: m.ModifiedAt,
            IsLoaded: true
        )).ToList();
    }

    private async Task<IReadOnlyList<AvailableModel>> DiscoverLMStudioModelsAsync(CancellationToken ct)
    {
        // GET http://localhost:1234/v1/models
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            $"{_settings.Value.LMStudioEndpoint}/v1/models", ct);
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<OpenAIModelsResponse>(ct);
        
        return json!.Data.Select(m => new AvailableModel(
            Id: m.Id,
            Name: m.Id,
            Provider: ModelProvider.LMStudio,
            SizeBytes: null,
            ModifiedAt: null,
            IsLoaded: true
        )).ToList();
    }

    // Response DTOs
    private record OllamaTagsResponse(List<OllamaModel> Models);
    private record OllamaModel(string Name, long Size, DateTime ModifiedAt);
    private record OpenAIModelsResponse(List<OpenAIModel> Data);
    private record OpenAIModel(string Id);
}