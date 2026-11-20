namespace Suwageval.Contracts.Agents;

public sealed record ArchiveVersionsRequest(IReadOnlyList<Guid> VersionIds, bool Archived);
