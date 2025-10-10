namespace MAF.InsightStreamer.Domain.Exceptions;

/// <summary>
/// Exception thrown when a thread ID does not match the expected session ID.
/// </summary>
public class ThreadIdMismatchException : Exception
{
    /// <summary>
    /// Gets the thread ID that was provided.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the session ID that was expected.
    /// </summary>
    public Guid ExpectedSessionId { get; }

    /// <summary>
    /// Gets the actual session ID associated with the thread.
    /// </summary>
    public Guid? ActualSessionId { get; }

    /// <summary>
    /// Initializes a new instance of the ThreadIdMismatchException class.
    /// </summary>
    /// <param name="threadId">The thread ID that was provided.</param>
    /// <param name="expectedSessionId">The session ID that was expected.</param>
    /// <param name="actualSessionId">The actual session ID associated with the thread.</param>
    public ThreadIdMismatchException(string threadId, Guid expectedSessionId, Guid? actualSessionId)
        : base($"Thread ID '{threadId}' does not match session ID '{expectedSessionId}'. " +
               (actualSessionId.HasValue ? $"Actual session ID: '{actualSessionId.Value}'." : "Thread not found."))
    {
        ThreadId = threadId;
        ExpectedSessionId = expectedSessionId;
        ActualSessionId = actualSessionId;
    }

    /// <summary>
    /// Initializes a new instance of the ThreadIdMismatchException class with a custom message.
    /// </summary>
    /// <param name="threadId">The thread ID that was provided.</param>
    /// <param name="expectedSessionId">The session ID that was expected.</param>
    /// <param name="actualSessionId">The actual session ID associated with the thread.</param>
    /// <param name="message">Custom error message.</param>
    public ThreadIdMismatchException(string threadId, Guid expectedSessionId, Guid? actualSessionId, string message)
        : base(message)
    {
        ThreadId = threadId;
        ExpectedSessionId = expectedSessionId;
        ActualSessionId = actualSessionId;
    }

    /// <summary>
    /// Initializes a new instance of the ThreadIdMismatchException class with a custom message and inner exception.
    /// </summary>
    /// <param name="threadId">The thread ID that was provided.</param>
    /// <param name="expectedSessionId">The session ID that was expected.</param>
    /// <param name="actualSessionId">The actual session ID associated with the thread.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public ThreadIdMismatchException(string threadId, Guid expectedSessionId, Guid? actualSessionId, string message, Exception innerException)
        : base(message, innerException)
    {
        ThreadId = threadId;
        ExpectedSessionId = expectedSessionId;
        ActualSessionId = actualSessionId;
    }
}