namespace Suwageval.Server.Evaluation.Runs;

internal sealed record RegressionHistoryRow(
    Guid ScenarioId,
    Guid CheckId,
    Guid RunId,
    DateTime CreatedAt,
    int Samples,
    int SamplesPassed,
    double PassThreshold,
    string? DataVersion = null
);
