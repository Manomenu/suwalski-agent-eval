namespace Suwageval.Server.Evaluation.Runs;

internal sealed record CheckResultRow(
    Guid ScenarioResultId,
    string CheckKey,
    int CheckRevision,
    int LatestRevision,
    string Definition,
    double PassThreshold,
    bool Passed,
    bool NotJudged,
    string Evidence,
    string Message,
    Guid CheckId,
    string? JudgeModel
);
