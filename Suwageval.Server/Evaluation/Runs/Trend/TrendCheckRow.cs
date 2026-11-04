namespace Suwageval.Server.Evaluation.Runs.Trend;

internal sealed record TrendCheckRow(Guid RunId, string ScenarioKey, string CheckKey, int Samples, int SamplesPassed, double PassThreshold);
