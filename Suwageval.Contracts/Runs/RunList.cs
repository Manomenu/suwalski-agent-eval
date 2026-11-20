namespace Suwageval.Contracts.Runs;

public sealed record RunList
{
    public required IReadOnlyList<RunSummary> Runs { get; init; }

    public required int Page { get; init; }

    public required int TotalCount { get; init; }
}
