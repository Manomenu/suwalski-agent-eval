namespace Suwageval.Contracts.Runs;

/// <summary>
/// A check that passed in some finished runs of one agent version and failed in others: the model does not answer the same way
/// twice, so a failure of this check in a run is noise as much as a regression. A check counts per run by its threshold over the
/// samples; an edited check (another revision) is another check.
/// </summary>
public sealed record UnstableCheck
{
    public required string ScenarioKey { get; init; }

    public required string CheckKey { get; init; }

    public required int CheckRevision { get; init; }

    public required Guid AgentVersionId { get; init; }

    /// <summary>The label the version's contract carries, if any.</summary>
    public string? VersionLabel { get; init; }

    /// <summary>The finished runs of the version that had a verdict of the check; at least 2.</summary>
    public required int Runs { get; init; }

    /// <summary>How many of <see cref="Runs"/> the check passed in; more than 0 and less than <see cref="Runs"/>.</summary>
    public required int PassedRuns { get; init; }

    /// <summary>The newest of those runs in which the check failed.</summary>
    public required Guid NewestFailedRunId { get; init; }
}
