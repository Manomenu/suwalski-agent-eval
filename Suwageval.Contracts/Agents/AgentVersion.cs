namespace Suwageval.Contracts.Agents;

/// <summary>
/// A registered version of an agent, without its content (but with the names of the models its LLM agents use, so runs can be
/// filtered by model, the version of its retrieval data and its description, if it has any);
/// <c>ArchivedAt</c> is set while the version is archived. <c>GroupRunsOnly</c> is true when the version has runs and every one is in a
/// group (run --group; a re-check counts as in the group of the run it was made from): an experiment's version, like a sweep's
/// variant, hidden by default as the runs in groups are. A version with no runs, or one run outside a group, is not.
/// <c>Runs</c> counts the version's runs in no group and <c>GroupRuns</c> those in a group (a re-check counts as in the group of its original):
/// where the version is used. Both zero: unused.
/// </summary>
public sealed record AgentVersion(
    Guid Id,
    Guid AgentId,
    string ContentHash,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ModelNames,
    string? Label = null,
    DateTimeOffset? ArchivedAt = null,
    string? DataVersion = null,
    string? DataDescription = null
)
{
    public required bool GroupRunsOnly { get; init; }

    public required int Runs { get; init; }

    public required int GroupRuns { get; init; }
}
