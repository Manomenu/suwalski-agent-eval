namespace Suwageval.Server.Evaluation.Runs;

internal sealed record NewCheckResult(
    Guid CheckId,
    int CheckRevision,
    bool Passed,
    string EvidenceJson,
    string Message,
    bool NotJudged = false,
    string? JudgeModel = null
);
