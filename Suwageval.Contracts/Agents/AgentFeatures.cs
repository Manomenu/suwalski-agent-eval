namespace Suwageval.Contracts.Agents;

/// <summary>
/// Which areas of Suwageval an agent uses (<c>features:</c> in <c>agent.yaml</c>). The file is the source of truth: a push carries it, and
/// the server refuses what a switched-off area would store. Left out, an agent has evaluation and no monitoring.
/// </summary>
public sealed record AgentFeatures
{
    /// <summary>Scenarios, runs and comparisons; on unless set to false.</summary>
    public bool Evaluation { get; init; } = true;

    /// <summary>
    /// Conversations from a deployed agent and the checks run on them. Absent: off, and the server refuses the agent's conversations;
    /// present (even empty, <c>monitoring: {}</c>): on, with these settings. Switching it on is the agent's repository agreeing that its
    /// conversations are stored.
    /// </summary>
    public MonitoringSettings? Monitoring { get; init; }
}
