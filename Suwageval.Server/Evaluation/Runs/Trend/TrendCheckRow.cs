namespace Suwageval.Server.Evaluation.Runs.Trend;

/// <summary>One check of one scenario in one run, counted over the run's samples: how many had a verdict, how many passed.</summary>
internal sealed record TrendCheckRow(Guid RunId, string ScenarioKey, string CheckKey, int Samples, int SamplesPassed, double PassThreshold);
