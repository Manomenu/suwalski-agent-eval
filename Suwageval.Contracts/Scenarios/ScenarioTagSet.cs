namespace Suwageval.Contracts.Scenarios;

/// <summary>The tags of one scenario.</summary>
public sealed record ScenarioTagSet(string Key, IReadOnlyList<string> Tags);
