namespace Suwageval.Contracts.Scenarios;

/// <summary>Where a scenario came from, when it was made from something real: a monitored conversation (its id in Suwageval).</summary>
public sealed record ScenarioOrigin
{
    public Guid? MonitoringConversation { get; init; }
}
