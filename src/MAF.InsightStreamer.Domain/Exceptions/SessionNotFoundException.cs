namespace MAF.InsightStreamer.Domain.Exceptions;

/// <summary>
/// Exception thrown when a document session is not found.
/// </summary>
public class SessionNotFoundException : Exception
{
    /// <summary>
    /// Gets the session ID that was not found.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Initializes a new instance of the SessionNotFoundException class.
    /// </summary>
    /// <param name="sessionId">The session ID that was not found.</param>
    public SessionNotFoundException(Guid sessionId)
        : base($"Document session with ID '{sessionId}' was not found.")
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// Initializes a new instance of the SessionNotFoundException class with a custom message.
    /// </summary>
    /// <param name="sessionId">The session ID that was not found.</param>
    /// <param name="message">Custom error message.</param>
    public SessionNotFoundException(Guid sessionId, string message)
        : base(message)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// Initializes a new instance of the SessionNotFoundException class with a custom message and inner exception.
    /// </summary>
    /// <param name="sessionId">The session ID that was not found.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public SessionNotFoundException(Guid sessionId, string message, Exception innerException)
        : base(message, innerException)
    {
        SessionId = sessionId;
    }
}