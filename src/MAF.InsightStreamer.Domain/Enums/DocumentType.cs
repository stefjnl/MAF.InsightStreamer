namespace MAF.InsightStreamer.Domain.Enums;

/// <summary>
/// Represents the supported types of documents for analysis.
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Portable Document Format file.
    /// </summary>
    Pdf,
    
    /// <summary>
    /// Microsoft Word document.
    /// </summary>
    Word,
    
    /// <summary>
    /// Markdown formatted text file.
    /// </summary>
    Markdown,
    
    /// <summary>
    /// Plain text file without formatting.
    /// </summary>
    PlainText,
    
    /// <summary>
    /// Unknown or unsupported document type.
    /// </summary>
    Unknown
}