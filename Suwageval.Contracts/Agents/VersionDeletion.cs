namespace Suwageval.Contracts.Agents;

public sealed record VersionDeletion(Guid VersionId, int Runs, int Samples, int Verdicts, string? Label = null, int Conversations = 0);
