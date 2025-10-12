using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MAF.InsightStreamer.Application.Interfaces;
using MAF.InsightStreamer.Domain.Models;
using System.Threading;

namespace MAF.InsightStreamer.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IDocumentSessionService using IMemoryCache.
/// Manages document sessions with sliding expiration and automatic cleanup.
/// </summary>
public class DocumentSessionService : IDocumentSessionService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IThreadManagementService _threadManagementService;
    private readonly ILogger<DocumentSessionService> _logger;
    private const string SessionKeyPrefix = "session:";
    private const int SessionExpirationMinutes = 15;

    /// <summary>
    /// Initializes a new instance of the DocumentSessionService class.
    /// </summary>
    /// <param name="memoryCache">The memory cache for storing sessions.</param>
    /// <param name="threadManagementService">The thread management service for cleanup.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public DocumentSessionService(
        IMemoryCache memoryCache,
        IThreadManagementService threadManagementService,
        ILogger<DocumentSessionService> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _threadManagementService = threadManagementService ?? throw new ArgumentNullException(nameof(threadManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new document session with the provided analysis result and chunks.
    /// </summary>
    /// <param name="analysisResult">The analysis result of the document.</param>
    /// <param name="chunks">The list of document chunks for Q&A reference.</param>
    /// <returns>A new DocumentSession instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when analysisResult or chunks is null.</exception>
    public async Task<DocumentSession> CreateSessionAsync(DocumentAnalysisResult analysisResult, List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (analysisResult == null)
        {
            throw new ArgumentNullException(nameof(analysisResult));
        }

        if (chunks == null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        _logger.LogInformation("Creating new document session for document type: {DocumentType}", analysisResult.DocumentType);

        var session = new DocumentSession(
            analysisResult.Metadata,
            analysisResult,
            chunks,
            1); // 1 hour expiration (was incorrectly 0 due to integer division)

        var cacheKey = GetSessionCacheKey(session.SessionId);
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(SessionExpirationMinutes))
            .RegisterPostEvictionCallback(OnSessionEvicted);

        _memoryCache.Set(cacheKey, session, cacheEntryOptions);

        // Create a thread for this session
        await _threadManagementService.CreateThreadForDocumentAsync(session.SessionId, cancellationToken);

        _logger.LogInformation("Successfully created document session with ID: {SessionId}", session.SessionId);
        return session;
    }

    /// <summary>
    /// Retrieves an existing document session by its identifier.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session.</param>
    /// <returns>The DocumentSession if found, null otherwise.</returns>
    public Task<DocumentSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            _logger.LogWarning("Attempted to retrieve session with empty GUID");
            return Task.FromResult<DocumentSession?>(null);
        }

        var cacheKey = GetSessionCacheKey(sessionId);
        
        if (_memoryCache.TryGetValue(cacheKey, out DocumentSession? session))
        {
            _logger.LogDebug("Successfully retrieved session with ID: {SessionId}", sessionId);
            return Task.FromResult(session);
        }

        _logger.LogWarning("Session not found with ID: {SessionId}", sessionId);
        return Task.FromResult<DocumentSession?>(null);
    }

    /// <summary>
    /// Updates the expiration time for a session, extending its TTL on Q&A activity.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task UpdateSessionExpirationAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            _logger.LogWarning("Attempted to update expiration for session with empty GUID");
            return Task.CompletedTask;
        }

        var cacheKey = GetSessionCacheKey(sessionId);
        
        if (_memoryCache.TryGetValue(cacheKey, out DocumentSession? session))
        {
            if (session != null)
            {
                // Update the session's expiration time
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(SessionExpirationMinutes);
                
                // Refresh the cache entry to reset sliding expiration
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(SessionExpirationMinutes))
                    .RegisterPostEvictionCallback(OnSessionEvicted);

                _memoryCache.Set(cacheKey, session, cacheEntryOptions);
                
                _logger.LogDebug("Updated expiration for session with ID: {SessionId}", sessionId);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to update expiration for non-existent session with ID: {SessionId}", sessionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a document session from storage and cleans up associated resources.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            _logger.LogWarning("Attempted to remove session with empty GUID");
            return;
        }

        var cacheKey = GetSessionCacheKey(sessionId);
        
        if (_memoryCache.TryGetValue(cacheKey, out DocumentSession? session))
        {
            if (session != null)
            {
                // Remove the session from cache
                _memoryCache.Remove(cacheKey);
                
                // Remove the associated thread if it exists
                var threadId = $"{sessionId}";
                await _threadManagementService.RemoveThreadAsync(threadId, cancellationToken);
                
                _logger.LogInformation("Successfully removed session with ID: {SessionId}", sessionId);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to remove non-existent session with ID: {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Callback method invoked when a session is evicted from cache.
    /// Performs cleanup of associated resources.
    /// </summary>
    /// <param name="key">The cache key of the evicted item.</param>
    /// <param name="value">The value that was evicted.</param>
    /// <param name="reason">The reason for eviction.</param>
    /// <param name="state">The state object.</param>
    private void OnSessionEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is DocumentSession session)
        {
            _logger.LogInformation("Session evicted from cache with ID: {SessionId}, Reason: {Reason}",
                session.SessionId, reason);

            // Remove the associated thread using fire-and-forget with proper exception handling
            var threadId = $"{session.SessionId}";
            _ = Task.Run(async () =>
            {
                try
                {
                    await _threadManagementService.RemoveThreadAsync(threadId, default);
                    _logger.LogDebug("Successfully removed thread for evicted session: {SessionId}", session.SessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing thread for evicted session: {SessionId}", session.SessionId);
                }
            });
        }
    }

    /// <summary>
    /// Generates the cache key for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The cache key for the session.</returns>
    private static string GetSessionCacheKey(Guid sessionId)
    {
        return $"{SessionKeyPrefix}{sessionId}";
    }
}