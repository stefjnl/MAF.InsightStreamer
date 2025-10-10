using System;

namespace MAF.InsightStreamer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for document processing functionality.
/// </summary>
public class DocumentProcessingSettings
{
    /// <summary>
    /// Gets or sets the maximum file size in bytes for document uploads.
    /// Default is 10MB (10,485,760 bytes).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10485760;

    /// <summary>
    /// Gets or sets the array of allowed file types for document processing.
    /// Default includes pdf, docx, md, and txt files.
    /// </summary>
    public string[] AllowedFileTypes { get; set; } = { "pdf", "docx", "md", "txt" };

    /// <summary>
    /// Gets or sets the chunk size for document text splitting.
    /// Default is 4000 characters.
    /// </summary>
    public int ChunkSize { get; set; } = 4000;

    /// <summary>
    /// Gets or sets the chunk overlap size for document text splitting.
    /// Default is 400 characters.
    /// </summary>
    public int ChunkOverlap { get; set; } = 400;

    /// <summary>
    /// Gets or sets the session expiration time in minutes.
    /// Default is 15 minutes.
    /// </summary>
    public int SessionExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum number of questions allowed per session.
    /// Default is 50 questions.
    /// </summary>
    public int MaxQuestionsPerSession { get; set; } = 50;
}