using Microsoft.Extensions.AI;
using MAF.InsightStreamer.Application.Configuration;

namespace MAF.InsightStreamer.Infrastructure.Providers;

public interface IChatClientFactory
{
    IChatClient CreateClient(ProviderConfiguration config);
}