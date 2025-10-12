namespace MAF.InsightStreamer.Infrastructure.Providers;

public class ProviderSettings
{
    public const string SectionName = "OpenRouter";

    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public required string Endpoint { get; init; }

    // NEW: YouTube API configuration
    public required string YouTubeApiKey { get; init; }
}