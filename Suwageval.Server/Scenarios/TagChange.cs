namespace Suwageval.Server.Scenarios;

/// <summary>The outcome of a tag change: the scenarios it applied to, and the keys that matched none.</summary>
internal sealed record TagChange(IReadOnlyList<ScenarioTags> Changed, IReadOnlyList<string> UnknownKeys);
