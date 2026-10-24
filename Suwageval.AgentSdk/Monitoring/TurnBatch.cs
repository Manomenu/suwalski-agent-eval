using Suwageval.AgentSdk.Agents;

namespace Suwageval.AgentSdk.Monitoring;

/// <summary>
/// What a monitored agent sends to <c>POST /api/monitoring/turns</c>: turns of its conversations, with the version that answered them
/// named by its contract hash (<see cref="AgentContractHash.Of"/>). When the server does not know the hash it answers
/// <see cref="TurnBatchAnswer.SendContract"/> and stores nothing; the same turns come again with <see cref="Contract"/>, from which
/// the server registers the version, as a run does.
/// </summary>
public sealed record TurnBatch
{
    /// <summary>The agent's name in Suwageval (<c>name</c> in its <c>agent.yaml</c>).</summary>
    public required string Agent { get; init; }

    /// <summary>The environment the agent runs in (<c>Suwageval:Environment</c>), one its <c>features.monitoring.environments</c> takes.</summary>
    public required string Environment { get; init; }

    public required string ContractHash { get; init; }

    /// <summary>The whole contract, sent only when the server asked for it.</summary>
    public AgentContract? Contract { get; init; }

    public required IReadOnlyList<MonitoredTurn> Turns { get; init; }
}
