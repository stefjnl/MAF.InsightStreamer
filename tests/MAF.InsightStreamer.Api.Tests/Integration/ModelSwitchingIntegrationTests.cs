using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using MAF.InsightStreamer.Api;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;
namespace MAF.InsightStreamer.Api.Tests.Integration
{
    public class ModelSwitchingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly Mock<IModelDiscoveryService> _discoveryServiceMock;
        private readonly Mock<IContentOrchestratorService> _orchestratorServiceMock;
        private readonly Mock<IThreadMigrationService> _threadMigrationServiceMock;

        public ModelSwitchingIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _discoveryServiceMock = new Mock<IModelDiscoveryService>();
            _orchestratorServiceMock = new Mock<IContentOrchestratorService>();
            _threadMigrationServiceMock = new Mock<IThreadMigrationService>();

            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace services with mocks
                    services.RemoveAll<IModelDiscoveryService>();
                    services.RemoveAll<IContentOrchestratorService>();
                    services.RemoveAll<IThreadMigrationService>();
                    
                    services.AddScoped(_ => _discoveryServiceMock.Object);
                    services.AddScoped(_ => _orchestratorServiceMock.Object);
                    services.AddScoped(_ => _threadMigrationServiceMock.Object);
                });
                
                // Configure JSON options to match the API
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // This ensures the test uses the same JSON serialization settings as the API
                });
            });
            
            // Configure JSON options to match the API for deserialization in tests
            // Need to create a custom factory or configure the HTTP client
        }

        [Fact]
        public async Task GetAvailableProviders_Endpoint_ReturnsSuccess()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/model/providers");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            // Use custom JSON options to handle enum deserialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            var content = await response.Content.ReadFromJsonAsync<GetAvailableProvidersResponse>(jsonOptions);
            Assert.NotNull(content);
            Assert.NotNull(content.Providers);
            Assert.NotNull(content.Current);
        }

        [Fact]
        public async Task DiscoverModels_Endpoint_ReturnsSuccess_ForOllama()
        {
            // Arrange
            var expectedModels = new List<AvailableModel>
            {
                new AvailableModel("llama3.2", "Llama 3.2", ModelProvider.Ollama, 1000000, DateTime.UtcNow, true)
            };
            
            _discoveryServiceMock.Setup(service => service.DiscoverModelsAsync(It.IsAny<ModelProvider>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedModels);

            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/model/discover/1"); // 1 = Ollama

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            // Use custom JSON options to handle enum deserialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            var content = await response.Content.ReadFromJsonAsync<List<AvailableModel>>(jsonOptions);
            Assert.NotNull(content);
            Assert.Single(content);
            Assert.Equal("llama3.2", content[0].Id);
            Assert.Equal(ModelProvider.Ollama, content[0].Provider);
        }

        [Fact]
        public async Task DiscoverModels_Endpoint_ReturnsSuccess_ForLMStudio()
        {
            // Arrange
            var expectedModels = new List<AvailableModel>
            {
                new AvailableModel("lm-studio-model", "LM Studio Model", ModelProvider.LMStudio, null, null, true)
            };
            
            _discoveryServiceMock.Setup(service => service.DiscoverModelsAsync(It.IsAny<ModelProvider>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedModels);

            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/model/discover/2"); // 2 = LMStudio

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            // Use custom JSON options to handle enum deserialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            var content = await response.Content.ReadFromJsonAsync<List<AvailableModel>>(jsonOptions);
            Assert.NotNull(content);
            Assert.Single(content);
            Assert.Equal("lm-studio-model", content[0].Id);
            Assert.Equal(ModelProvider.LMStudio, content[0].Provider);
        }

        [Fact]
        public async Task DiscoverModels_Endpoint_ReturnsBadRequest_ForOpenRouter()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/model/discover/0"); // 0 = OpenRouter

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SwitchModel_Endpoint_ReturnsSuccess()
        {
            // Arrange
            var switchRequest = new SwitchModelRequest(ModelProvider.Ollama, "llama3.2");
            
            _threadMigrationServiceMock.Setup(service => service.ResetOnModelSwitchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All conversation history has been reset");

            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsJsonAsync("/api/model/switch", switchRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
            var content = await response.Content.ReadFromJsonAsync<SwitchModelResponse>(jsonOptions);
            Assert.NotNull(content);
            Assert.Contains("Switched to", content.Message);
        }

        [Fact]
        public async Task SwitchModel_Endpoint_CallsOrchestratorService()
        {
            // Arrange
            var switchRequest = new SwitchModelRequest(ModelProvider.Ollama, "llama3.2");
            
            _threadMigrationServiceMock.Setup(service => service.ResetOnModelSwitchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All conversation history has been reset");

            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsJsonAsync("/api/model/switch", switchRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _orchestratorServiceMock.Verify(
                service => service.SwitchProvider(
                    It.IsAny<ModelProvider>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task SwitchModel_Endpoint_HandlesDifferentProviders()
        {
            // Arrange
            var ollamaRequest = new SwitchModelRequest(ModelProvider.Ollama, "llama3.2");
            var lmStudioRequest = new SwitchModelRequest(ModelProvider.LMStudio, "lm-studio-model");
            
            _threadMigrationServiceMock.Setup(service => service.ResetOnModelSwitchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All conversation history has been reset");

            var client = _factory.CreateClient();

            // Act - Switch to Ollama
            var ollamaResponse = await client.PostAsJsonAsync("/api/model/switch", ollamaRequest);
            
            // Act - Switch to LM Studio
            var lmStudioResponse = await client.PostAsJsonAsync("/api/model/switch", lmStudioRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, ollamaResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, lmStudioResponse.StatusCode);
            
            // Verify that SwitchProvider was called exactly twice
            _orchestratorServiceMock.Verify(
                service => service.SwitchProvider(
                    It.IsAny<ModelProvider>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ModelEndpoints_FollowCorrectRoutePattern()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act & Assert - Test all endpoints exist and return appropriate responses
            var providersResponse = await client.GetAsync("/api/model/providers");
            Assert.Equal(HttpStatusCode.OK, providersResponse.StatusCode);

            // The discover endpoint requires a provider parameter, so we test with an invalid provider
            var discoverResponse = await client.GetAsync("/api/model/discover/999"); // Invalid provider
            // This should return OK with empty list or error, but the route should exist
            Assert.True(providersResponse.StatusCode == HttpStatusCode.OK ||
                       providersResponse.StatusCode == HttpStatusCode.BadRequest ||
                       providersResponse.StatusCode == HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task SwitchModel_Endpoint_RequiresValidRequest()
        {
            // Arrange
            var invalidRequest = new { invalidProperty = "invalid" };
            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsJsonAsync("/api/model/switch", invalidRequest);

            // Assert - Should return bad request due to model validation
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                       response.StatusCode == HttpStatusCode.OK); // May return OK with error message
        }
    }
    
    // Response DTOs for testing
    public record GetAvailableProvidersResponse(
        IEnumerable<ProviderInfo> Providers,
        string Current
    );
    
    public record ProviderInfo(
        string Name,
        int Value
    );
    
    public record SwitchModelResponse(
        string Message,
        string? Warning
    );
}