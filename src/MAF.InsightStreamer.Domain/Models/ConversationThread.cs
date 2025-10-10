namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents a conversation thread for document Q&A interactions.
/// This is a domain abstraction that hides the infrastructure implementation details.
/// </summary>
public class ConversationThread
{
    /// <summary>
    /// Gets the unique identifier for the thread.
    /// </summary>
    public string ThreadId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the session identifier this thread belongs to.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Gets the creation timestamp of the thread.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Initializes a new instance of the ConversationThread class.
    /// </summary>
    public ConversationThread()
    {
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Initializes a new instance of the ConversationThread class with specified values.
    /// </summary>
    /// <param name="threadId">The thread identifier</param>
    /// <param name="sessionId">The session identifier</param>
    public ConversationThread(string threadId, Guid sessionId)
    {
        ThreadId = threadId ?? throw new ArgumentNullException(nameof(threadId));
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;
    }
}