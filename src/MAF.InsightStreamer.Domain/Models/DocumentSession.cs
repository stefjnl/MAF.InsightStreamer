namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents a session for conversational Q&A about a specific document.
/// </summary>
public class DocumentSession
{
    /// <summary>
    /// Gets or sets the unique identifier for this session.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the metadata of the document being analyzed in this session.
    /// </summary>
    public DocumentMetadata DocumentMetadata { get; set; } = null!;

    /// <summary>
    /// Gets or sets the analysis result of the document.
    /// </summary>
    public DocumentAnalysisResult AnalysisResult { get; set; } = null!;

    /// <summary>
    /// Gets or sets the list of document chunks used for Q&A reference.
    /// </summary>
    public List<DocumentChunk> DocumentChunks { get; set; } = new();

    /// <summary>
    /// Gets or sets the conversation history for this session.
    /// </summary>
    public List<ConversationMessage> ConversationHistory { get; set; } = new();

    /// <summary>
    /// Gets or sets the total tokens used in this session.
    /// </summary>
    public long TotalTokensUsed { get; set; } = 0;

    /// <summary>
    /// Gets or sets the timestamp when this session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this session expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Initializes a new instance of the DocumentSession class.
    /// </summary>
    public DocumentSession()
    {
        SessionId = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddHours(24); // Default 24-hour expiration
    }

    /// <summary>
    /// Initializes a new instance of the DocumentSession class with specified values.
    /// </summary>
    /// <param name="documentMetadata">The metadata of the document.</param>
    /// <param name="analysisResult">The analysis result of the document.</param>
    /// <param name="documentChunks">The list of document chunks.</param>
    /// <param name="expirationHours">The number of hours until the session expires. Default is 24 hours.</param>
    public DocumentSession(
        DocumentMetadata documentMetadata,
        DocumentAnalysisResult analysisResult,
        List<DocumentChunk> documentChunks,
        int expirationHours = 24)
    {
        SessionId = Guid.NewGuid();
        DocumentMetadata = documentMetadata ?? throw new ArgumentNullException(nameof(documentMetadata));
        AnalysisResult = analysisResult ?? throw new ArgumentNullException(nameof(analysisResult));
        DocumentChunks = documentChunks ?? throw new ArgumentNullException(nameof(documentChunks));
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddHours(expirationHours);
    }

    /// <summary>
    /// Gets a value indicating whether this session has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}