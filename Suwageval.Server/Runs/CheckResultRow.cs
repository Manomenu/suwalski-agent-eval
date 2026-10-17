namespace Suwageval.Server.Runs;

/// <summary>A row of <c>check_results</c> with the key and pass threshold of its check (as of the revision that ran).</summary>
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
