namespace Suwageval.Contracts.Scenarios;

/// <summary>A scenario with its latest revision's content; <c>Scenario.Tags</c> are the tags it carries now.</summary>
public sealed record ScenarioDetail(int Revision, DateTimeOffset CreatedAt, ScenarioInput Scenario);
