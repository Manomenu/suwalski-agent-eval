namespace Suwageval.Contracts.Agents;

/// <summary>
/// What deleting an archived version removes with it: the runs (re-checks of them included), their samples and the verdicts on those, and
/// the monitored conversations it answered a turn of (a conversation goes whole, not with a gap).
/// </summary>
public sealed record VersionDeletion(Guid VersionId, int Runs, int Samples, int Verdicts, string? Label = null, int Conversations = 0);
