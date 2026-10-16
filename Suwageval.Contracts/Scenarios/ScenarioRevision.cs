namespace Suwageval.Contracts.Scenarios;

/// <summary>One revision of a scenario. Tags are no part of a revision: <c>Scenario.Tags</c> is empty.</summary>
public sealed record ScenarioRevision(int Revision, string ContentHash, DateTimeOffset CreatedAt, ScenarioInput Scenario);
