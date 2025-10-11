// Application/Configuration/ProviderConfiguration.cs
using MAF.InsightStreamer.Domain.Enums;

namespace MAF.InsightStreamer.Application.Configuration
{
    public class ProviderConfiguration
    {
        public ModelProvider Provider { get; set; }
        public string? ApiKey { get; set; }  // Null for local
        public required string Endpoint { get; set; }
        public required string Model { get; set; }
    }
}