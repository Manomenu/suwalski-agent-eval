namespace Suwageval.Server.Runs.Trend;

/// <summary>A scenario a run ran (it has a sample), with or without checks.</summary>
internal sealed record TrendScenarioRow(Guid RunId, string ScenarioKey);
