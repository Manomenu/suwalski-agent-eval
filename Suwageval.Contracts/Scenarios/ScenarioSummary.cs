namespace Suwageval.Contracts.Scenarios;

public sealed record ScenarioSummary(string Key, IReadOnlyList<string> Tags, int Revision, DateTimeOffset CreatedAt);
