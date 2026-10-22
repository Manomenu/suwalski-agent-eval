namespace Suwageval.Contracts.Runs;

/// <summary>One page of runs, newest first.</summary>
public sealed record RunList
{
    public required IReadOnlyList<RunSummary> Runs { get; init; }

    /// <summary>The page these runs are (1-based): the one asked for, or the last one when that is past the end.</summary>
    public required int Page { get; init; }

    /// <summary>How many runs match the filters, over all pages.</summary>
    public required int TotalCount { get; init; }
}
