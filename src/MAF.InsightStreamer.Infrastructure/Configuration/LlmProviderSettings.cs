namespace MAF.InsightStreamer.Infrastructure.Configuration
{
    public class LlmProviderSettings
    {
        public const string SectionName = "LlmProvider";

        public required string ApiKey { get; init; }
        public required string Model { get; init; }
        public required string Endpoint { get; init; }
    }
}
