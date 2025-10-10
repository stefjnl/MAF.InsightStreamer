using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Infrastructure.Providers;
using Xunit;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services
{
    public class ChatClientFactoryTests
    {
        private readonly Mock<IOptionsMonitor<ModelDiscoverySettings>> _discoverySettingsMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<ChatClientFactory>> _loggerMock;
        private readonly ChatClientFactory _chatClientFactory;

        public ChatClientFactoryTests()
        {
            _discoverySettingsMock = new Mock<IOptionsMonitor<ModelDiscoverySettings>>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<ChatClientFactory>>();

            _chatClientFactory = new ChatClientFactory(
                _discoverySettingsMock.Object,
                _configurationMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public void CreateClient_CreatesOpenRouterClient_WhenProviderIsOpenRouter()
        {
            // Arrange
            var config = new ProviderConfiguration
            {
                Provider = ModelProvider.OpenRouter,
                ApiKey = "test-api-key",
                Endpoint = "https://openrouter.ai/api/v1",
                Model = "test-model"
            };

            // Act
            var result = _chatClientFactory.CreateClient(config);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void CreateClient_ThrowsInvalidOperationException_WhenOpenRouterHasNoApiKey()
        {
            // Arrange
            var config = new ProviderConfiguration
            {
                Provider = ModelProvider.OpenRouter,
                ApiKey = null, // No API key
                Endpoint = "https://openrouter.ai/api/v1",
                Model = "test-model"
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _chatClientFactory.CreateClient(config));
        }

        [Fact]
        public void CreateClient_CreatesOllamaClient_WhenProviderIsOllama()
        {
            // Arrange
            var config = new ProviderConfiguration
            {
                Provider = ModelProvider.Ollama,
                ApiKey = null, // Ollama doesn't require API key
                Endpoint = "http://localhost:11434/v1",
                Model = "llama3.2"
            };

            // Act
            var result = _chatClientFactory.CreateClient(config);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void CreateClient_CreatesLMStudioClient_WhenProviderIsLMStudio()
        {
            // Arrange
            var config = new ProviderConfiguration
            {
                Provider = ModelProvider.LMStudio,
                ApiKey = null, // LM Studio doesn't require API key
                Endpoint = "http://localhost:1234/v1",
                Model = "lm-studio-model"
            };

            // Act
            var result = _chatClientFactory.CreateClient(config);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void CreateClient_ThrowsArgumentException_WhenProviderIsUnsupported()
        {
            // Arrange
            var config = new ProviderConfiguration
            {
                Provider = (ModelProvider)(-1), // Invalid provider
                ApiKey = "test-key",
                Endpoint = "http://example.com",
                Model = "test-model"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _chatClientFactory.CreateClient(config));
        }

        [Fact]
        public void CreateClient_HandlesDifferentEndpointsCorrectly()
        {
            // Test OpenRouter endpoint
            var openRouterConfig = new ProviderConfiguration
            {
                Provider = ModelProvider.OpenRouter,
                ApiKey = "test-key",
                Endpoint = "https://custom-openrouter.com/api/v1",
                Model = "custom-model"
            };
            var openRouterClient = _chatClientFactory.CreateClient(openRouterConfig);
            Assert.NotNull(openRouterClient);

            // Test Ollama endpoint
            var ollamaConfig = new ProviderConfiguration
            {
                Provider = ModelProvider.Ollama,
                ApiKey = null,
                Endpoint = "http://custom-ollama:11434/v1",
                Model = "custom-ollama-model"
            };
            var ollamaClient = _chatClientFactory.CreateClient(ollamaConfig);
            Assert.NotNull(ollamaClient);

            // Test LM Studio endpoint
            var lmStudioConfig = new ProviderConfiguration
            {
                Provider = ModelProvider.LMStudio,
                ApiKey = null,
                Endpoint = "http://custom-lmstudio:1234/v1",
                Model = "custom-lmstudio-model"
            };
            var lmStudioClient = _chatClientFactory.CreateClient(lmStudioConfig);
            Assert.NotNull(lmStudioClient);
        }

        [Fact]
        public void CreateClient_HandlesDifferentModelsCorrectly()
        {
            // Test different models for each provider
            var openRouterConfig = new ProviderConfiguration
            {
                Provider = ModelProvider.OpenRouter,
                ApiKey = "test-key",
                Endpoint = "https://openrouter.ai/api/v1",
                Model = "google/gemini-2.5-flash"
            };
            var openRouterClient = _chatClientFactory.CreateClient(openRouterConfig);
            Assert.NotNull(openRouterClient);

            var ollamaConfig = new ProviderConfiguration
            {
                Provider = ModelProvider.Ollama,
                ApiKey = null,
                Endpoint = "http://localhost:11434/v1",
                Model = "mistral:latest"
            };
            var ollamaClient = _chatClientFactory.CreateClient(ollamaConfig);
            Assert.NotNull(ollamaClient);

            var lmStudioConfig = new ProviderConfiguration
            {
                Provider = ModelProvider.LMStudio,
                ApiKey = null,
                Endpoint = "http://localhost:1234/v1",
                Model = "microsoft/DialoGPT-medium"
            };
            var lmStudioClient = _chatClientFactory.CreateClient(lmStudioConfig);
            Assert.NotNull(lmStudioClient);
        }
    }
}