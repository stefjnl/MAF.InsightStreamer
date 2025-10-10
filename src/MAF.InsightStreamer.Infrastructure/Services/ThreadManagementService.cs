using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI.Chat;
using System.Linq;
using System.Collections.Concurrent;
using MAF.InsightStreamer.Infrastructure.Providers;

namespace MAF.InsightStreamer.Infrastructure.Services;

/// <summary>
/// Implementation of thread management service for document Q&A interactions.
/// Manages conversation thread lifecycle using memory cache with 5-minute expiration.
/// </summary>
public class ThreadManagementService : IThreadManagementService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ThreadManagementService> _logger;
    private readonly IOptions<ProviderSettings> _settings;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    
    // Track session mappings separately since IMemoryCache doesn't expose keys
    private readonly ConcurrentDictionary<string, string> _sessionToThreadMap = new();

    /// <summary>
    /// Initializes a new instance of the ThreadManagementService class.
    /// </summary>
    /// <param name="cache">The memory cache for storing threads</param>
    /// <param name="logger">The logger instance for recording service operations</param>
    /// <param name="settings">The provider settings for configuring chat clients</param>
    public ThreadManagementService(IMemoryCache cache, ILogger<ThreadManagementService> logger, IOptions<ProviderSettings> settings)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Creates a new thread for a document session.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the document session</param>
    /// <returns>A unique thread identifier</returns>
    /// <exception cref="ArgumentException">Thrown when sessionId is empty</exception>
    public async Task<string> CreateThreadForDocumentAsync(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            _logger.LogError("SessionId cannot be empty");
            throw new ArgumentException("SessionId cannot be empty", nameof(sessionId));
        }

        var threadId = Guid.NewGuid().ToString();
        
        // Create a new AIAgent for this thread
        var agent = CreateDefaultAgent();
        
        // Create the domain model
        var thread = new ConversationThread(threadId, sessionId);
        
        // Store the thread with expiration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheExpiration,
            SlidingExpiration = _cacheExpiration
        };
        
        // Store both the agent and the metadata
        var agentCacheKey = $"thread:{threadId}";
        var metadataCacheKey = $"thread:{threadId}:metadata";
        
        _cache.Set(agentCacheKey, agent, cacheOptions);
        _cache.Set(metadataCacheKey, thread, cacheOptions);
        
        // Store the reverse mapping for cleanup
        var sessionKey = $"session:{sessionId}:thread";
        _sessionToThreadMap[sessionKey] = threadId;
        
        _logger.LogInformation("Created new thread {ThreadId} for session {SessionId}", threadId, sessionId);
        
        return threadId;
    }

    /// <summary>
    /// Retrieves an existing thread by its identifier.
    /// </summary>
    /// <param name="threadId">The thread identifier</param>
    /// <returns>ConversationThread instance if found, null otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when threadId is null or empty</exception>
    public async Task<ConversationThread?> GetThreadAsync(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            _logger.LogError("ThreadId cannot be null or empty");
            throw new ArgumentException("ThreadId cannot be null or empty", nameof(threadId));
        }

        var agentCacheKey = $"thread:{threadId}";
        var metadataCacheKey = $"thread:{threadId}:metadata";
        
        if (_cache.TryGetValue(agentCacheKey, out AIAgent? agent) &&
            _cache.TryGetValue(metadataCacheKey, out ConversationThread? metadata))
        {
            _logger.LogDebug("Retrieved thread {ThreadId} from cache", threadId);
            return metadata;
        }
        
        _logger.LogWarning("Thread {ThreadId} not found in cache", threadId);
        return null;
    }

    /// <summary>
    /// Removes a thread from storage.
    /// </summary>
    /// <param name="threadId">The thread identifier to remove</param>
    /// <exception cref="ArgumentException">Thrown when threadId is null or empty</exception>
    public async Task RemoveThreadAsync(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            _logger.LogError("ThreadId cannot be null or empty");
            throw new ArgumentException("ThreadId cannot be null or empty", nameof(threadId));
        }

        var agentCacheKey = $"thread:{threadId}";
        var metadataCacheKey = $"thread:{threadId}:metadata";
        
        // Remove the thread from cache
        _cache.Remove(agentCacheKey);
        _cache.Remove(metadataCacheKey);
        
        // Find and remove the session mapping
        var keysToRemove = _sessionToThreadMap
            .Where(kvp => kvp.Value == threadId)
            .Select(kvp => kvp.Key)
            .ToList();
        
        // Remove the session mappings
        foreach (var key in keysToRemove)
        {
            _sessionToThreadMap.TryRemove(key, out _);
        }
        
        _logger.LogInformation("Removed thread {ThreadId} and {MappingCount} session mappings", threadId, keysToRemove.Count);
    }

    /// <summary>
    /// Gets the underlying AIAgent for a thread (for internal use by orchestrator).
    /// </summary>
    /// <param name="threadId">The thread identifier</param>
    /// <returns>The AIAgent instance if found, null otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when threadId is null or empty</exception>
    public async Task<object?> GetAgentAsync(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            _logger.LogError("ThreadId cannot be null or empty");
            throw new ArgumentException("ThreadId cannot be null or empty", nameof(threadId));
        }

        var cacheKey = $"thread:{threadId}";
        
        if (_cache.TryGetValue(cacheKey, out AIAgent? agent))
        {
            _logger.LogDebug("Retrieved agent for thread {ThreadId} from cache", threadId);
            return agent;
        }
        
        _logger.LogWarning("Agent for thread {ThreadId} not found in cache", threadId);
        return null;
    }

    /// <summary>
    /// Creates a default AIAgent instance for conversational Q&A.
    /// Configures the agent with appropriate tools and instructions for document-based conversations.
    /// </summary>
    /// <returns>A configured AIAgent instance</returns>
    private AIAgent CreateDefaultAgent()
    {
        var config = _settings.Value;

        // Create ChatClient similar to ContentOrchestratorService
        ChatClient chatClient = new(
            model: config.Model,
            credential: new ApiKeyCredential(config.ApiKey),
            options: new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(config.Endpoint)
            }
        );

        // Convert to IChatClient
        IChatClient client = chatClient.AsIChatClient();

        // Create the agent with Q&A-focused instructions
        var agent = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                Name = "DocumentQAAgent",
                Instructions = "You are a helpful assistant that answers questions about documents. " +
                             "Use the provided document context to answer questions accurately. " +
                             "Maintain conversation context across multiple questions about the same document. " +
                             "When answering, indicate which parts of the document were most relevant to your answer.",
                ChatOptions = new ChatOptions
                {
                    // Tools can be added here as needed for document analysis
                    Tools = []
                }
            }
        );
        
        return agent;
    }
}