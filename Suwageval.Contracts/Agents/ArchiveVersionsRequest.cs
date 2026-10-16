namespace Suwageval.Contracts.Agents;

/// <summary>Archives (<c>Archived: true</c>) or restores (<c>false</c>) versions of one agent; nothing is deleted.</summary>
public sealed record ArchiveVersionsRequest(IReadOnlyList<Guid> VersionIds, bool Archived);
