namespace Suwageval.Server.Runs;

/// <summary>A check's verdict on one sample, ready to be stored.</summary>
internal sealed record NewCheckResult(Guid CheckId, int CheckRevision, bool Passed, string EvidenceJson, string Message);
