namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents the result of a question-answering operation about a document.
/// </summary>
public class QuestionAnswerResult
{
    /// <summary>
    /// Gets or sets the answer generated for the asked question.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of indices of document chunks that were most relevant to answering the question.
    /// </summary>
    public List<int> RelevantChunkIndices { get; set; } = new();

    /// <summary>
    /// Gets or sets the conversation history including the current question and answer.
    /// </summary>
    public List<ConversationMessage> ConversationHistory { get; set; } = new();

    /// <summary>
    /// Gets or sets the unique identifier for the conversation thread.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the QuestionAnswerResult class.
    /// </summary>
    public QuestionAnswerResult()
    {
        ThreadId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Initializes a new instance of the QuestionAnswerResult class with specified values.
    /// </summary>
    /// <param name="answer">The answer generated for the asked question.</param>
    /// <param name="relevantChunkIndices">The list of indices of relevant document chunks.</param>
    /// <param name="conversationHistory">The conversation history including current Q&A.</param>
    /// <param name="threadId">The unique identifier for the conversation thread. If null, a new GUID will be generated.</param>
    public QuestionAnswerResult(
        string answer,
        List<int> relevantChunkIndices,
        List<ConversationMessage> conversationHistory,
        string? threadId = null)
    {
        Answer = answer ?? throw new ArgumentNullException(nameof(answer));
        RelevantChunkIndices = relevantChunkIndices ?? throw new ArgumentNullException(nameof(relevantChunkIndices));
        ConversationHistory = conversationHistory ?? throw new ArgumentNullException(nameof(conversationHistory));
        ThreadId = threadId ?? Guid.NewGuid().ToString();
    }
}