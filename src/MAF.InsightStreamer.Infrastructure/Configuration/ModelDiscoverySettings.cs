namespace MAF.InsightStreamer.Infrastructure.Configuration;

public class ModelDiscoverySettings
{
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string LMStudioEndpoint { get; set; } = "http://localhost:1234";
    public bool EnableAutoDiscovery { get; set; } = true;
    public int DiscoveryCacheMinutes { get; set; } = 5;
}