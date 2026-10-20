namespace Suwageval.Contracts.Runs;

/// <summary>
/// A row of the per-tag summary: <see cref="Tag"/> is absent for the row of every check of the run. A check counts once, however many
/// samples it has; a run has a rate for a tag only when it ran every scenario of the tag.
/// </summary>
public sealed record ComparedTag
{
    public string? Tag { get; init; }

    /// <summary>One per compared run, in the order asked for.</summary>
    public required IReadOnlyList<ComparedRate> Rates { get; init; }
}
