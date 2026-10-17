namespace Suwageval.Server.Runs;

/// <summary>A check's verdict on one sample, ready to be stored. <paramref name="JudgeModel"/>: the model that was asked; null for a deterministic check.</summary>
internal sealed record NewCheckResult(
    Guid CheckId,
    int CheckRevision,
    bool Passed,
    string EvidenceJson,
    string Message,
    bool NotJudged = false,
    string? JudgeModel = null
);
