using MAF.InsightStreamer.Domain.Models;

namespace MAF.InsightStreamer.Application.Interfaces;

/// <summary>
/// Provides functionality for asking questions about analyzed documents in a conversational context.
/// Orchestrates between document sessions, thread management, and content analysis services.
/// </summary>
public interface IQuestionAnswerService
{
    /// <summary>
    /// Asks a question about a previously analyzed document within a conversational context.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the document session.</param>
    /// <param name="question">The question to ask about the document.</param>
    /// <param name="threadId">Optional thread identifier for maintaining conversation context. If null, a new thread will be created.</param>
    /// <returns>A QuestionAnswerResult containing the answer and relevant context.</returns>
    /// <exception cref="Domain.Exceptions.SessionNotFoundException">Thrown when the session is not found.</exception>
    /// <exception cref="Domain.Exceptions.SessionExpiredException">Thrown when the session has expired.</exception>
    /// <exception cref="Domain.Exceptions.ThreadIdMismatchException">Thrown when the thread ID does not match the session.</exception>
    /// <exception cref="Domain.Exceptions.RateLimitExceededException">Thrown when the rate limit for questions is exceeded.</exception>
    Task<QuestionAnswerResult> AskQuestionAsync(
        Guid sessionId,
        string question,
        string? threadId = null);
}