using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Application.Configuration;
using MAF.InsightStreamer.Infrastructure.Services;
using Xunit;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services
{
    public class ModelDiscoveryServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IMemoryCache> _memoryCacheMock;
        private readonly Mock<IOptions<ModelDiscoverySettings>> _settingsMock;
        private readonly Mock<ILogger<ModelDiscoveryService>> _loggerMock;
        private readonly ModelDiscoveryService _modelDiscoveryService;

        public ModelDiscoveryServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _memoryCacheMock = new Mock<IMemoryCache>();
            _settingsMock = new Mock<IOptions<ModelDiscoverySettings>>();
            _loggerMock = new Mock<ILogger<ModelDiscoveryService>>();
            
            _settingsMock.Setup(s => s.Value).Returns(new ModelDiscoverySettings
            {
                OllamaEndpoint = "http://localhost:11434",
                LMStudioEndpoint = "http://localhost:1234"
            });

            // Set up the cache to properly handle Set calls
            _memoryCacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns(Mock.Of<ICacheEntry>);
            
            _modelDiscoveryService = new ModelDiscoveryService(
                _httpClientFactoryMock.Object,
                _memoryCacheMock.Object,
                _settingsMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task DiscoverModels_Ollama_ReturnsAvailableModels()
        {
            // Arrange
            var ollamaResponse = new
            {
                models = new[]
                {
                    new { name = "llama3.2", size = 1000000L, modified_at = DateTime.UtcNow }
                }
            };
            var jsonResponse = JsonSerializer.Serialize(ollamaResponse);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.Ollama);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal("llama3.2", result[0].Id);
            Assert.Equal(ModelProvider.Ollama, result[0].Provider);
            Assert.Equal(1000000L, result[0].SizeBytes);
        }

        [Fact]
        public async Task DiscoverModels_LMStudio_ReturnsAvailableModels()
        {
            // Arrange
            var lmStudioResponse = new
            {
                data = new[]
                {
                    new { id = "lm-studio-model" }
                }
            };
            var jsonResponse = JsonSerializer.Serialize(lmStudioResponse);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.LMStudio);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal("lm-studio-model", result[0].Id);
            Assert.Equal(ModelProvider.LMStudio, result[0].Provider);
        }

        [Fact]
        public async Task DiscoverModels_UsesCache_WhenCacheHit()
        {
            // Arrange
            var cachedModels = new List<AvailableModel>
            {
                new AvailableModel("cached-model", "Cached Model", ModelProvider.Ollama, 1000000, DateTime.UtcNow, true)
            };

            object cacheValue = cachedModels;
            _memoryCacheMock.Setup(cache => cache.TryGetValue($"models:{ModelProvider.Ollama}", out cacheValue))
                .Returns(true);

            // Act
            var result = await _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.Ollama);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cached-model", result[0].Id);
            _httpClientFactoryMock.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DiscoverModels_DoesNotUseCache_WhenCacheMiss()
        {
            // Arrange
            object cacheValue;
            _memoryCacheMock.Setup(cache => cache.TryGetValue($"models:{ModelProvider.Ollama}", out cacheValue))
                .Returns(false);

            var ollamaResponse = new
            {
                models = new[]
                {
                    new { name = "llama3.2", size = 1000000L, modified_at = DateTime.UtcNow }
                }
            };
            var jsonResponse = JsonSerializer.Serialize(ollamaResponse);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.Ollama);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _httpClientFactoryMock.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Once);
            _memoryCacheMock.Verify(cache => cache.CreateEntry(It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task DiscoverModels_ThrowsNotSupportedException_ForUnsupportedProvider()
        {
            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(
                () => _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.OpenAI));
        }

        [Fact]
        public async Task ValidateEndpointAsync_ReturnsTrue_WhenEndpointIsAccessible()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _modelDiscoveryService.ValidateEndpointAsync("http://localhost:11434");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateEndpointAsync_ReturnsFalse_WhenEndpointIsNotAccessible()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException());

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _modelDiscoveryService.ValidateEndpointAsync("http://localhost:11434");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DiscoverModels_HandlesOllamaApiError()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                () => _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.Ollama));
        }

        [Fact]
        public async Task DiscoverModels_HandlesLMStudioApiError()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                () => _modelDiscoveryService.DiscoverModelsAsync(ModelProvider.LMStudio));
        }
    }
    
    // Mock HttpMessageHandler to handle HTTP requests in tests
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
    
        public MockHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "{}")
        {
            _response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            };
        }
    
        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }
    
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}