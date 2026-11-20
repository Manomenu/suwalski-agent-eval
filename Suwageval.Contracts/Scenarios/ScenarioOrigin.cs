namespace Suwageval.Contracts.Scenarios;

public sealed record ScenarioOrigin
{
    public Guid? MonitoringConversation { get; init; }
}
