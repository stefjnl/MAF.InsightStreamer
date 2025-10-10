using MAF.InsightStreamer.Domain.Enums;

namespace MAF.InsightStreamer.Domain.Models;

/// <summary>
/// Represents an exception that occurs during document parsing operations.
/// </summary>
public class DocumentParsingException : Exception
{
    /// <summary>
    /// Gets the type of document that failed to parse.
    /// </summary>
    public DocumentType DocumentType { get; }

    /// <summary>
    /// Initializes a new instance of the DocumentParsingException class.
    /// </summary>
    /// <param name="documentType">The type of document that failed to parse.</param>
    public DocumentParsingException(DocumentType documentType)
        : base($"Failed to parse document of type {documentType}.")
    {
        DocumentType = documentType;
    }

    /// <summary>
    /// Initializes a new instance of the DocumentParsingException class with a custom error message.
    /// </summary>
    /// <param name="documentType">The type of document that failed to parse.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public DocumentParsingException(DocumentType documentType, string message)
        : base(message)
    {
        DocumentType = documentType;
    }

    /// <summary>
    /// Initializes a new instance of the DocumentParsingException class with a custom error message and inner exception.
    /// </summary>
    /// <param name="documentType">The type of document that failed to parse.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DocumentParsingException(DocumentType documentType, string message, Exception innerException)
        : base(message, innerException)
    {
        DocumentType = documentType;
    }
}