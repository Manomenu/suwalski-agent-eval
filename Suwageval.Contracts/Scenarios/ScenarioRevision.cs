namespace Suwageval.Contracts.Scenarios;

public sealed record ScenarioRevision(int Revision, string ContentHash, DateTimeOffset CreatedAt, ScenarioInput Scenario);
