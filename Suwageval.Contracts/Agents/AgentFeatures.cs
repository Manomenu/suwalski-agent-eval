namespace Suwageval.Contracts.Agents;

public sealed record AgentFeatures
{
    public bool Evaluation { get; init; } = true;

    public MonitoringSettings? Monitoring { get; init; }
}
