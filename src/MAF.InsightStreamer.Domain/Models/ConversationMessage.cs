using MAF.InsightStreamer.Domain.Enums;

namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents a single message in a conversation about a document.
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// Gets or sets the role of the message sender (User or Assistant).
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the optional list of chunk indices referenced in this message.
    /// </summary>
    public List<int>? ChunkReferences { get; set; }

    /// <summary>
    /// Initializes a new instance of the ConversationMessage class.
    /// </summary>
    public ConversationMessage()
    {
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Initializes a new instance of the ConversationMessage class with specified values.
    /// </summary>
    /// <param name="role">The role of the message sender.</param>
    /// <param name="content">The content of the message.</param>
    /// <param name="chunkReferences">Optional list of chunk indices referenced in this message.</param>
    public ConversationMessage(MessageRole role, string content, List<int>? chunkReferences = null)
    {
        Role = role;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Timestamp = DateTime.UtcNow;
        ChunkReferences = chunkReferences;
    }
}