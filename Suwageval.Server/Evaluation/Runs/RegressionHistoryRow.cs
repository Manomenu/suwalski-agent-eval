namespace Suwageval.Server.Evaluation.Runs;

/// <summary>One check of one scenario in one earlier run of its history, counted over that run's samples; rows come oldest first.</summary>
internal sealed record RegressionHistoryRow(
    Guid ScenarioId,
    Guid CheckId,
    Guid RunId,
    DateTime CreatedAt,
    int Samples,
    int SamplesPassed,
    double PassThreshold
);
