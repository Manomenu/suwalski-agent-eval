namespace Suwageval.Contracts.Scenarios;

/// <summary>A scenario as listed: its latest revision's number, without the conversation.</summary>
public sealed record ScenarioSummary(string Key, IReadOnlyList<string> Tags, int Revision, DateTimeOffset CreatedAt);
