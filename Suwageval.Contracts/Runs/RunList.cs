namespace Suwageval.Contracts.Runs;

/// <summary>One page of runs, newest first.</summary>
public sealed record RunList
{
    public required IReadOnlyList<RunSummary> Runs { get; init; }

    /// <summary>The run id to pass as <c>before</c> to get the next page; null when there are no older runs.</summary>
    public Guid? Next { get; init; }
}
