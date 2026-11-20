using Suwageval.AgentSdk.Agents;

namespace Suwageval.AgentSdk.Monitoring;

public sealed record TurnBatch
{
    public required string Agent { get; init; }

    public required string Environment { get; init; }

    public required string ContractHash { get; init; }

    public AgentContract? Contract { get; init; }

    public required IReadOnlyList<MonitoredTurn> Turns { get; init; }
}
