namespace Suwageval.Server.Evaluation.Runs.Trend;

/// <summary>The turn time of one sample that made a call.</summary>
internal sealed record TrendDurationRow(Guid RunId, string ScenarioKey, int DurationMs);
