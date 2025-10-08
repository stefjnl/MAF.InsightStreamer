namespace MAF.InsightStreamer.Application.DTOs;

public class AnalyzeRequest
{
    public required string VideoUrl { get; init; }
    public bool IncludeTimestamps { get; init; } = true;
}