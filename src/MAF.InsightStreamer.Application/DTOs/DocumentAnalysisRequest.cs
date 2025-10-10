namespace MAF.InsightStreamer.Application.DTOs;

/// <summary>
/// Represents a request for document analysis with specified parameters.
/// </summary>
public record DocumentAnalysisRequest
{
    /// <summary>
    /// Gets the name of the file to be analyzed.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of analysis to perform on the document.
    /// Valid values are: "summary", "key_points", "custom".
    /// </summary>
    public string AnalysisType { get; init; } = "summary";

    /// <summary>
    /// Gets the custom analysis request when AnalysisType is set to "custom".
    /// This property is nullable and only used for custom analysis types.
    /// </summary>
    public string? CustomRequest { get; init; }
}