namespace MAF.InsightStreamer.Domain.Exceptions;

/// <summary>
/// Exception thrown when the rate limit for questions in a session is exceeded.
/// </summary>
public class RateLimitExceededException : Exception
{
    /// <summary>
    /// Gets the session ID that exceeded the rate limit.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the maximum number of questions allowed per session.
    /// </summary>
    public int MaxQuestionsPerSession { get; }

    /// <summary>
    /// Gets the current number of questions asked in the session.
    /// </summary>
    public int CurrentQuestionCount { get; }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class.
    /// </summary>
    /// <param name="sessionId">The session ID that exceeded the rate limit.</param>
    /// <param name="maxQuestionsPerSession">The maximum number of questions allowed per session.</param>
    /// <param name="currentQuestionCount">The current number of questions asked in the session.</param>
    public RateLimitExceededException(Guid sessionId, int maxQuestionsPerSession, int currentQuestionCount)
        : base($"Rate limit exceeded for session '{sessionId}'. Maximum of {maxQuestionsPerSession} questions allowed, but {currentQuestionCount} have been asked.")
    {
        SessionId = sessionId;
        MaxQuestionsPerSession = maxQuestionsPerSession;
        CurrentQuestionCount = currentQuestionCount;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with a custom message.
    /// </summary>
    /// <param name="sessionId">The session ID that exceeded the rate limit.</param>
    /// <param name="maxQuestionsPerSession">The maximum number of questions allowed per session.</param>
    /// <param name="currentQuestionCount">The current number of questions asked in the session.</param>
    /// <param name="message">Custom error message.</param>
    public RateLimitExceededException(Guid sessionId, int maxQuestionsPerSession, int currentQuestionCount, string message)
        : base(message)
    {
        SessionId = sessionId;
        MaxQuestionsPerSession = maxQuestionsPerSession;
        CurrentQuestionCount = currentQuestionCount;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with a custom message and inner exception.
    /// </summary>
    /// <param name="sessionId">The session ID that exceeded the rate limit.</param>
    /// <param name="maxQuestionsPerSession">The maximum number of questions allowed per session.</param>
    /// <param name="currentQuestionCount">The current number of questions asked in the session.</param>
    /// <param name="message">Custom error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public RateLimitExceededException(Guid sessionId, int maxQuestionsPerSession, int currentQuestionCount, string message, Exception innerException)
        : base(message, innerException)
    {
        SessionId = sessionId;
        MaxQuestionsPerSession = maxQuestionsPerSession;
        CurrentQuestionCount = currentQuestionCount;
    }
}