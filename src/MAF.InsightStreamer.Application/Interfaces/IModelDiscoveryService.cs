using MAF.InsightStreamer.Domain.Enums;
using MAF.InsightStreamer.Application.DTOs;

namespace MAF.InsightStreamer.Application.Interfaces;

public interface IModelDiscoveryService
{
    Task<IReadOnlyList<AvailableModel>> DiscoverModelsAsync(
        ModelProvider provider, 
        CancellationToken ct = default);
    
    Task<bool> ValidateEndpointAsync(
        string endpoint, 
        CancellationToken ct = default);
}