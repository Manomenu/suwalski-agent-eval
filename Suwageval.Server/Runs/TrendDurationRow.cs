namespace Suwageval.Server.Runs;

/// <summary>The turn time of one sample that made a call.</summary>
internal sealed record TrendDurationRow(Guid RunId, string ScenarioKey, int DurationMs);
