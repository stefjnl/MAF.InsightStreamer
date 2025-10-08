namespace MAF.InsightStreamer.Application.DTOs;

public class VideoResponse
{
    public required string VideoId { get; init; }
    public required string Summary { get; init; }
    public TimeSpan Duration { get; init; }
}