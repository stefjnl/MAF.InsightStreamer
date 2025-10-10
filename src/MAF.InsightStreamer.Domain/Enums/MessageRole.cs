namespace MAF.InsightStreamer.Domain.Enums;

/// <summary>
/// Represents the role of a participant in a conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// Represents the user asking questions or providing input.
    /// </summary>
    User,

    /// <summary>
    /// Represents the assistant providing answers or responses.
    /// </summary>
    Assistant
}