namespace Suwageval.Contracts.Runs;

public sealed record BaselineComparison
{
    public required Guid RunId { get; init; }

    public required IReadOnlyList<ComparedRun> Runs { get; init; }

    public required IReadOnlyList<ComparedTag> Tags { get; init; }

    public required IReadOnlyList<ComparedCheck> Checks { get; init; }
}
