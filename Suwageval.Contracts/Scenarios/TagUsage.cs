namespace Suwageval.Contracts.Scenarios;

/// <summary>A tag in use and how many live scenarios carry it.</summary>
public sealed record TagUsage(string Tag, int ScenarioCount);
