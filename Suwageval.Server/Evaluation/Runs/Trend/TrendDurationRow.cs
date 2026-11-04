namespace Suwageval.Server.Evaluation.Runs.Trend;

internal sealed record TrendDurationRow(Guid RunId, string ScenarioKey, int DurationMs);
