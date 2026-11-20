namespace Suwageval.Contracts.Scenarios;

public sealed record ScenarioDetail(int Revision, DateTimeOffset CreatedAt, ScenarioInput Scenario);
