namespace Suwageval.Contracts.Agents;

public sealed record AgentVersion(
    Guid Id,
    Guid AgentId,
    string ContentHash,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ModelNames,
    string? Label = null,
    DateTimeOffset? ArchivedAt = null
)
{
    public IReadOnlyList<RetrievalData> Data { get; init; } = [];

    public required int Runs { get; init; }

    public required int GroupRuns { get; init; }

    public IReadOnlyList<VersionMonitoring> Monitoring { get; init; } = [];
}
