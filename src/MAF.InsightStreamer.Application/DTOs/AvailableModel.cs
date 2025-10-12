using MAF.InsightStreamer.Domain.Enums;

namespace MAF.InsightStreamer.Application.DTOs;

public record AvailableModel(
    string Id,
    string Name,
    ModelProvider Provider,
    long? SizeBytes,
    DateTime? ModifiedAt,
    bool IsLoaded  // For Ollama
);