using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MAF.InsightStreamer.Application.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace MAF.InsightStreamer.Infrastructure.Providers;

public class ChatClientFactory : IChatClientFactory
{
    private readonly IOptionsMonitor<ModelDiscoverySettings> _discoverySettings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatClientFactory> _logger;

    public ChatClientFactory(
        IOptionsMonitor<ModelDiscoverySettings> discoverySettings,
        IConfiguration configuration,
        ILogger<ChatClientFactory> logger)
    {
        _discoverySettings = discoverySettings;
        _configuration = configuration;
        _logger = logger;
    }

    public IChatClient CreateClient(ProviderConfiguration config)
    {
        return config.Provider switch
        {
            ModelProvider.OpenRouter => CreateOpenRouterClient(config),
            ModelProvider.Ollama => CreateOllamaClient(config),
            ModelProvider.LMStudio => CreateLMStudioClient(config),
            _ => throw new ArgumentException($"Unsupported provider: {config.Provider}")
        };
    }

    private IChatClient CreateOpenRouterClient(ProviderConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
            throw new InvalidOperationException("OpenRouter requires API key");

        var client = new OpenAIClient(new ApiKeyCredential(config.ApiKey), new OpenAIClientOptions() { Endpoint = new Uri(config.Endpoint) });
        var chatClient = client.GetChatClient(config.Model);

        return chatClient.AsIChatClient();
    }

    private IChatClient CreateOllamaClient(ProviderConfiguration config)
    {
        // Ollama uses OpenAI-compatible API
        var client = new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions() { Endpoint = new Uri(config.Endpoint) });  // Some implementations require a key, even if ignored
        var chatClient = client.GetChatClient(config.Model);

        return chatClient.AsIChatClient();
    }

    private IChatClient CreateLMStudioClient(ProviderConfiguration config)
    {
        // LM Studio is OpenAI-compatible
        var client = new OpenAIClient(new ApiKeyCredential("lm-studio"), new OpenAIClientOptions() { Endpoint = new Uri(config.Endpoint) });
        var chatClient = client.GetChatClient(config.Model);

        return chatClient.AsIChatClient();
    }
}