namespace MAF.InsightStreamer.Application.DTOs;

/// <summary>
/// Represents the JSON response structure from the content orchestrator service.
/// </summary>
public record OrchestratorAnalysisResult
{
    /// <summary>
    /// Gets or sets the summary of the document content.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of key points extracted from the document.
    /// </summary>
    public List<string> KeyPoints { get; init; } = new();
}