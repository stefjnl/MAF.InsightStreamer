using MAF.InsightStreamer.Domain.Models;

namespace MAF.InsightStreamer.Application.Interfaces;

/// <summary>
/// Manages the lifecycle of document sessions for conversational Q&A interactions.
/// Provides session creation, retrieval, expiration management, and cleanup functionality.
/// </summary>
public interface IDocumentSessionService
{
    /// <summary>
    /// Creates a new document session with the provided analysis result and chunks.
    /// </summary>
    /// <param name="analysisResult">The analysis result of the document.</param>
    /// <param name="chunks">The list of document chunks for Q&A reference.</param>
    /// <returns>A new DocumentSession instance.</returns>
    Task<DocumentSession> CreateSessionAsync(DocumentAnalysisResult analysisResult, List<DocumentChunk> chunks);

    /// <summary>
    /// Retrieves an existing document session by its identifier.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session.</param>
    /// <returns>The DocumentSession if found, null otherwise.</returns>
    Task<DocumentSession?> GetSessionAsync(Guid sessionId);

    /// <summary>
    /// Updates the expiration time for a session, extending its TTL on Q&A activity.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSessionExpirationAsync(Guid sessionId);

    /// <summary>
    /// Removes a document session from storage and cleans up associated resources.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveSessionAsync(Guid sessionId);
}