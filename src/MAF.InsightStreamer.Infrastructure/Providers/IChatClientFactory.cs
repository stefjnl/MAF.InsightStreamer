using Microsoft.Extensions.AI;

namespace MAF.InsightStreamer.Infrastructure.Providers;

public interface IChatClientFactory
{
    IChatClient CreateClient(ProviderConfiguration config);
}