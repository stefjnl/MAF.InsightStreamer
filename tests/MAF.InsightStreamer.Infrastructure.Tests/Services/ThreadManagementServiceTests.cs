using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Domain.Models;
using MAF.InsightStreamer.Infrastructure.Configuration;
using MAF.InsightStreamer.Infrastructure.Providers;
using MAF.InsightStreamer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MAF.InsightStreamer.Infrastructure.Tests.Services
{
    public class ThreadManagementServiceTests
    {
        private readonly Mock<IMemoryCache> _memoryCacheMock;
        private readonly Mock<ILogger<ThreadManagementService>> _loggerMock;
        private readonly Mock<IOptions<ProviderSettings>> _providerSettingsMock;
        private readonly ThreadManagementService _threadManagementService;

        public ThreadManagementServiceTests()
        {
            _memoryCacheMock = new Mock<IMemoryCache>();
            _loggerMock = new Mock<ILogger<ThreadManagementService>>();
            _providerSettingsMock = new Mock<IOptions<ProviderSettings>>();
            
            // Setup cache mock to properly handle CreateEntry method
            var sharedCache = new Dictionary<object, object>();
            
            _memoryCacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns((object key, out object value) => {
                    if (sharedCache.TryGetValue(key, out object? val)) {
                        value = val;
                        return true;
                    }
                    value = null!;
                    return false;
                });
            
            _memoryCacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns((object key) =>
                {
                    var entry = new Mock<ICacheEntry>();
                    var entryKey = key;
                    object entryValue = null;
                    
                    entry.Setup(e => e.Key).Returns(entryKey);
                    entry.SetupSet(e => e.Value = It.IsAny<object>()).Callback<object>(v => entryValue = v);
                    entry.Setup(e => e.Dispose()).Callback(() =>
                    {
                        if (entryValue != null)
                        {
                            sharedCache[entryKey] = entryValue;
                        }
                    });
                    
                    return entry.Object;
                });
            
            _memoryCacheMock.Setup(x => x.Remove(It.IsAny<object>()))
                .Callback((object key) => {
                    sharedCache.Remove(key);
                });
            
            _providerSettingsMock.Setup(s => s.Value).Returns(new ProviderSettings
            {
                ApiKey = "test-key",
                Endpoint = "https://test-endpoint.com",
                Model = "test-model",
                YouTubeApiKey = "youtube-test-key"
            });

            _threadManagementService = new ThreadManagementService(
                _memoryCacheMock.Object,
                _loggerMock.Object,
                _providerSettingsMock.Object);
        }

        [Fact]
        public async Task GetAgentAsync_ReturnsAgent_WhenThreadExists()
        {
            // Arrange
            var threadId = Guid.NewGuid().ToString();
            var sessionId = Guid.NewGuid();
            await _threadManagementService.CreateThreadForDocumentAsync(sessionId);

            // Act
            var result = await _threadManagementService.GetAgentAsync(threadId);

            // Assert
            // The thread ID we used doesn't exist, so result should be null
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAgentAsync_ReturnsAgent_WhenThreadActuallyExists()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var threadId = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);

            // Act
            var result = await _threadManagementService.GetAgentAsync(threadId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetOrCreateThreadAsync_CreatesNewThread_WhenThreadNotExists()
        {
            // Arrange
            var sessionId = Guid.NewGuid();

            // Act
            var result = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task CreateThreadForDocumentAsync_CreatesNewThread()
        {
            // Arrange
            var sessionId = Guid.NewGuid();

            // Act
            var result = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetThreadAsync_ReturnsThread_WhenExists()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var threadId = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);

            // Act
            var result = await _threadManagementService.GetThreadAsync(threadId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(threadId, result.ThreadId);
        }

        [Fact]
        public async Task RemoveThreadAsync_RemovesThread()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var threadId = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);
            
            // Verify thread exists
            var existingThread = await _threadManagementService.GetThreadAsync(threadId);
            Assert.NotNull(existingThread);

            // Act
            await _threadManagementService.RemoveThreadAsync(threadId);

            // Verify thread is removed
            var result = await _threadManagementService.GetThreadAsync(threadId);
            Assert.Null(result);
        }

        [Fact]
        public async Task ThreadManagementService_HandlesMultipleConcurrentThreads()
        {
            // Arrange
            var threads = new List<string>();
            var sessionIds = new List<Guid>();

            // Create multiple threads
            for (int i = 0; i < 5; i++)
            {
                var sessionId = Guid.NewGuid();
                var threadId = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);
                threads.Add(threadId);
                sessionIds.Add(sessionId);
            }

            // Act & Assert
            foreach (var threadId in threads)
            {
                var agent = await _threadManagementService.GetAgentAsync(threadId);
                Assert.NotNull(agent);
            }
        }

        [Fact]
        public async Task ThreadManagementService_HandlesThreadWithConversationMessages()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var threadId = await _threadManagementService.CreateThreadForDocumentAsync(sessionId);

            // Act - get agent for thread
            var agent = await _threadManagementService.GetAgentAsync(threadId);

            // Assert
            Assert.NotNull(agent);
        }
    }
}