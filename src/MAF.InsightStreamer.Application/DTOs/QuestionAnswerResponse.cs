using MAF.InsightStreamer.Domain.Models;

namespace MAF.InsightStreamer.Application.DTOs;

/// <summary>
/// Represents the response to a question about a document, including the answer and conversation context.
/// </summary>
public record QuestionAnswerResponse
{
    /// <summary>
    /// Gets the answer to the asked question.
    /// </summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of indices for document chunks that were most relevant to answering the question.
    /// </summary>
    public List<int> RelevantChunkIndices { get; init; } = new();

    /// <summary>
    /// Gets the thread identifier for maintaining conversation context across multiple questions.
    /// </summary>
    public string ThreadId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the complete conversation history for the current session, including the current question and answer.
    /// This allows the frontend to maintain conversation state without needing a separate endpoint.
    /// </summary>
    public List<ConversationMessage> ConversationHistory { get; init; } = new();

    /// <summary>
    /// Gets the total number of questions that have been asked in this session.
    /// </summary>
    public int TotalQuestionsAsked { get; init; }
}