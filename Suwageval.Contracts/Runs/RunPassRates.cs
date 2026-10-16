namespace Suwageval.Contracts.Runs;

/// <summary>
/// The pass rate of one finished run, over all its checks and per scenario tag: a point of the agent's trend. A check counts once
/// per scenario (passed or not by its threshold over the samples), the way the results page counts it.
/// </summary>
public sealed record RunPassRates
{
    public required Guid RunId { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The re-check whose results the point shows (the newest finished one of this run); null when the point shows the run's own
    /// results. A re-check is never a point of its own.
    /// </summary>
    public Guid? RecheckedBy { get; init; }

    public required Guid AgentVersionId { get; init; }

    /// <summary>The tag the run was limited to; null when it ran every scenario. Only such a run says what "all checks" are.</summary>
    public string? Tag { get; init; }

    /// <summary>The label the version's contract carries, if any.</summary>
    public string? VersionLabel { get; init; }

    /// <summary>The models the version's LLM agents use.</summary>
    public required IReadOnlyList<string> ModelNames { get; init; }

    /// <summary>Checks passed over checks, from 0 to 1.</summary>
    public required double PassRate { get; init; }

    /// <summary>One rate per tag the run's scenarios carry now, sorted by tag; a scenario without checks adds nothing to its tags.</summary>
    public required IReadOnlyList<TagPassRate> Tags { get; init; }
}
