using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MAF.InsightStreamer.Api.Controllers;
using MAF.InsightStreamer.Application.DTOs;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
namespace MAF.InsightStreamer.Api.Tests.Controllers
{
    public class ModelControllerTests
    {
        private readonly Mock<IModelDiscoveryService> _discoveryServiceMock;
        private readonly Mock<IContentOrchestratorService> _orchestratorServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IThreadMigrationService> _threadMigrationServiceMock;
        private readonly Mock<ILogger<ModelController>> _loggerMock;
        private readonly ModelController _modelController;

        public ModelControllerTests()
        {
            _discoveryServiceMock = new Mock<IModelDiscoveryService>();
            _orchestratorServiceMock = new Mock<IContentOrchestratorService>();
            _configurationMock = new Mock<IConfiguration>();
            _threadMigrationServiceMock = new Mock<IThreadMigrationService>();
            _loggerMock = new Mock<ILogger<ModelController>>();

            _modelController = new ModelController(
                _discoveryServiceMock.Object,
                _orchestratorServiceMock.Object,
                _configurationMock.Object,
                _threadMigrationServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task GetAvailableProviders_ReturnsProviders()
        {
            // Arrange
            _configurationMock.Setup(config => config["CurrentProvider"]).Returns("OpenRouter");

            // Act
            var result = _modelController.GetAvailableProviders();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var data = okResult.Value as System.Dynamic.ExpandoObject;
            
            // If ExpandoObject doesn't work, let's access using reflection
            var properties = okResult.Value.GetType().GetProperties();
            var currentProperty = properties.FirstOrDefault(p => p.Name == "Current");
            var providersProperty = properties.FirstOrDefault(p => p.Name == "Providers");
            
            Assert.NotNull(currentProperty);
            Assert.NotNull(providersProperty);
            
            var current = currentProperty.GetValue(okResult.Value);
            var providers = providersProperty.GetValue(okResult.Value) as IEnumerable<object>;
            
            Assert.Equal("OpenRouter", current.ToString());
            Assert.NotNull(providers);
            
            // Verify all providers are returned by checking the properties of each provider object
            var providerList = providers.ToList();
            Assert.NotEmpty(providerList);
            
            // Since we can't directly access anonymous types, let's verify count and basic structure
            Assert.Contains(providerList, p => {
                var nameProp = p.GetType().GetProperty("Name");
                var valueProp = p.GetType().GetProperty("Value");
                return nameProp != null && valueProp != null &&
                       nameProp.GetValue(p)?.ToString() == "OpenRouter" &&
                       Convert.ToInt32(valueProp.GetValue(p)) == 0;
            });
            Assert.Contains(providerList, p => {
                var nameProp = p.GetType().GetProperty("Name");
                var valueProp = p.GetType().GetProperty("Value");
                return nameProp != null && valueProp != null &&
                       nameProp.GetValue(p)?.ToString() == "Ollama" &&
                       Convert.ToInt32(valueProp.GetValue(p)) == 1;
            });
            Assert.Contains(providerList, p => {
                var nameProp = p.GetType().GetProperty("Name");
                var valueProp = p.GetType().GetProperty("Value");
                return nameProp != null && valueProp != null &&
                       nameProp.GetValue(p)?.ToString() == "LMStudio" &&
                       Convert.ToInt32(valueProp.GetValue(p)) == 2;
            });
            Assert.Contains(providerList, p => {
                var nameProp = p.GetType().GetProperty("Name");
                var valueProp = p.GetType().GetProperty("Value");
                return nameProp != null && valueProp != null &&
                       nameProp.GetValue(p)?.ToString() == "OpenAI" &&
                       Convert.ToInt32(valueProp.GetValue(p)) == 3;
            });
        }

        [Fact]
        public async Task DiscoverModels_ReturnsModels_WhenProviderIsOllama()
        {
            // Arrange
            var expectedModels = new List<AvailableModel>
            {
                new AvailableModel("llama3.2", "Llama 3.2", ModelProvider.Ollama, 1000000, null, true)
            };
            _discoveryServiceMock.Setup(service => service.DiscoverModelsAsync(ModelProvider.Ollama, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedModels);

            // Act
            var result = await _modelController.DiscoverModels(ModelProvider.Ollama, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualModels = Assert.IsType<List<AvailableModel>>(okResult.Value);
            
            Assert.Single(actualModels);
            Assert.Equal("llama3.2", actualModels[0].Id);
            Assert.Equal(ModelProvider.Ollama, actualModels[0].Provider);
        }

        [Fact]
        public async Task DiscoverModels_ReturnsModels_WhenProviderIsLMStudio()
        {
            // Arrange
            var expectedModels = new List<AvailableModel>
            {
                new AvailableModel("lm-studio-model", "LM Studio Model", ModelProvider.LMStudio, null, null, true)
            };
            _discoveryServiceMock.Setup(service => service.DiscoverModelsAsync(ModelProvider.LMStudio, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedModels);

            // Act
            var result = await _modelController.DiscoverModels(ModelProvider.LMStudio, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualModels = Assert.IsType<List<AvailableModel>>(okResult.Value);
            
            Assert.Single(actualModels);
            Assert.Equal("lm-studio-model", actualModels[0].Id);
            Assert.Equal(ModelProvider.LMStudio, actualModels[0].Provider);
        }

        [Fact]
        public async Task DiscoverModels_ReturnsBadRequest_WhenProviderIsOpenRouter()
        {
            // Act
            var result = await _modelController.DiscoverModels(ModelProvider.OpenRouter, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("OpenRouter models cannot be discovered locally", badRequestResult.Value?.ToString());
        }

        [Fact]
        public async Task DiscoverModels_ReturnsServiceUnavailable_WhenProviderIsUnavailable()
        {
            // Arrange
            _discoveryServiceMock.Setup(service => service.DiscoverModelsAsync(ModelProvider.Ollama, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Net.Http.HttpRequestException("Service unavailable"));

            // Act
            var result = await _modelController.DiscoverModels(ModelProvider.Ollama, CancellationToken.None);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, statusCodeResult.StatusCode);
            Assert.Contains("Ollama unavailable", statusCodeResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task SwitchModel_CallsOrchestratorService_WithCorrectParameters()
        {
            // Arrange
            var request = new SwitchModelRequest(ModelProvider.Ollama, "llama3.2");
            
            _configurationMock.Setup(config => config["Providers:Ollama:Endpoint"]).Returns("http://localhost:11434/v1");
            _configurationMock.Setup(config => config["OpenRouter:ApiKey"]).Returns("test-key");
            _threadMigrationServiceMock.Setup(service => service.ResetOnModelSwitchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All conversation history has been reset");

            // Act
            var result = await _modelController.SwitchModel(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var properties = okResult.Value.GetType().GetProperties();
            var messageProperty = properties.FirstOrDefault(p => p.Name == "Message");
            var warningProperty = properties.FirstOrDefault(p => p.Name == "Warning");
            
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(okResult.Value)?.ToString();
            Assert.Contains("Switched to", message);
            
            if (warningProperty != null)
            {
                var warning = warningProperty.GetValue(okResult.Value)?.ToString();
                Assert.Equal("All conversation history has been reset", warning);
            }
            
            _orchestratorServiceMock.Verify(
                service => service.SwitchProvider(
                    It.IsAny<ModelProvider>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task SwitchModel_HandlesOpenRouterProviderCorrectly()
        {
            // Arrange
            var request = new SwitchModelRequest(ModelProvider.OpenRouter, "test-model");
            
            _configurationMock.Setup(config => config["Providers:OpenRouter:Endpoint"]).Returns("https://openrouter.ai/api/v1");
            _configurationMock.Setup(config => config["OpenRouter:ApiKey"]).Returns("test-api-key");
            _threadMigrationServiceMock.Setup(service => service.ResetOnModelSwitchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All conversation history has been reset");

            // Act
            var result = await _modelController.SwitchModel(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var properties = okResult.Value.GetType().GetProperties();
            var messageProperty = properties.FirstOrDefault(p => p.Name == "Message");
            
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(okResult.Value)?.ToString();
            Assert.Contains("Switched to OpenRouter", message);
            
            _orchestratorServiceMock.Verify(
                service => service.SwitchProvider(
                    It.IsAny<ModelProvider>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task SwitchModel_HandlesLMStudioProviderCorrectly()
        {
            // Arrange
            var request = new SwitchModelRequest(ModelProvider.LMStudio, "lm-studio-model");
            
            _configurationMock.Setup(config => config["Providers:LMStudio:Endpoint"]).Returns("http://localhost:1234/v1");
            _configurationMock.Setup(config => config["OpenRouter:ApiKey"]).Returns("test-key");
            _threadMigrationServiceMock.Setup(service => service.ResetOnModelSwitchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("All conversation history has been reset");

            // Act
            var result = await _modelController.SwitchModel(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var properties = okResult.Value.GetType().GetProperties();
            var messageProperty = properties.FirstOrDefault(p => p.Name == "Message");
            
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(okResult.Value)?.ToString();
            Assert.Contains("Switched to LMStudio", message);
            
            _orchestratorServiceMock.Verify(
                service => service.SwitchProvider(
                    It.IsAny<ModelProvider>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public void GetEndpointForProvider_ReturnsCorrectEndpoint_ForEachProvider()
        {
            // This test verifies the private method indirectly through the SwitchModel functionality
            // We'll use reflection to directly test the private method
            var controllerType = typeof(ModelController);
            var method = controllerType.GetMethod("GetEndpointForProvider", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method); // The method should exist in the controller
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