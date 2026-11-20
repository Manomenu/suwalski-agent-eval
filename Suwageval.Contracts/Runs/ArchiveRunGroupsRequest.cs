namespace Suwageval.Contracts.Runs;

public sealed record ArchiveRunGroupsRequest(IReadOnlyList<string> Names, bool Archived);
