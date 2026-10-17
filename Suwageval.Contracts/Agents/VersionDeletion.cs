namespace Suwageval.Contracts.Agents;

/// <summary>What deleting an archived version removes with it: the runs (re-checks of them included), their samples and the verdicts on those.</summary>
public sealed record VersionDeletion(Guid VersionId, int Runs, int Samples, int Verdicts, string? Label = null);
