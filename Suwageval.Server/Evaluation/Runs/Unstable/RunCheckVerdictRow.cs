namespace Suwageval.Server.Evaluation.Runs.Unstable;

internal sealed record RunCheckVerdictRow(
    Guid RunId,
    DateTime CreatedAt,
    Guid AgentVersionId,
    string ScenarioKey,
    string CheckKey,
    int CheckRevision,
    int Samples,
    int SamplesPassed,
    double PassThreshold
);
