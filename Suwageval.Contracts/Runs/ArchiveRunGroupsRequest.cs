namespace Suwageval.Contracts.Runs;

/// <summary>Archives (<c>Archived: true</c>) or restores (<c>false</c>) run groups of one agent, by name; nothing is deleted.</summary>
public sealed record ArchiveRunGroupsRequest(IReadOnlyList<string> Names, bool Archived);
