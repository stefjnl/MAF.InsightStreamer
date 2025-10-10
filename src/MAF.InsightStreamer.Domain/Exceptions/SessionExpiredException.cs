namespace MAF.InsightStreamer.Domain.Exceptions;

/// <summary>
/// Exception thrown when a document session has expired.
/// </summary>
public class SessionExpiredException : Exception
{
    /// <summary>
    /// Gets the session ID that has expired.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the expiration time of the session.
    /// </summary>
    public DateTime ExpiresAt { get; }

    /// <summary>
    /// Initializes a new instance of the SessionExpiredException class.
    /// </summary>
    /// <param name="sessionId">The session ID that has expired.</param>
    /// <param name="expiresAt">The expiration time of the session.</param>
    public SessionExpiredException(Guid sessionId, DateTime expiresAt)
        : base($"Document session with ID '{sessionId}' expired on {expiresAt:yyyy-MM-dd HH:mm:ss UTC}.")
    {
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Initializes a new instance of the SessionExpiredException class with a custom message.
    /// </summary>
    /// <param name="sessionId">The session ID that has expired.</param>
    /// <param name="expiresAt">The expiration time of the session.</param>
    /// <param name="message">Custom error message.</param>
    public SessionExpiredException(Guid sessionId, DateTime expiresAt, string message)
        : base(message)
    {
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Initializes a new instance of the SessionExpiredException class with a custom message and inner exception.
    /// </summary>
    /// <param name="sessionId">The session ID that has expired.</param>
    /// <param name="expiresAt">The expiration time of the session.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public SessionExpiredException(Guid sessionId, DateTime expiresAt, string message, Exception innerException)
        : base(message, innerException)
    {
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }
}