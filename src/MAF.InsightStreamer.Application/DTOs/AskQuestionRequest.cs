namespace MAF.InsightStreamer.Application.DTOs;

/// <summary>
/// Represents a request to ask a question about a document in a specific session.
/// </summary>
public record AskQuestionRequest
{
    /// <summary>
    /// Gets the unique identifier for the document session.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Gets the question to be asked about the document.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional thread identifier for maintaining conversation context.
    /// This should be null for the first question in a session.
    /// </summary>
    public string? ThreadId { get; init; }
}