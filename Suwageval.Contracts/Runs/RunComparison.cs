namespace Suwageval.Contracts.Runs;

public sealed record RunComparison
{
    public required string Agent { get; init; }

    public string? Group { get; init; }

    public string? GroupNote { get; init; }

    public required IReadOnlyList<VariantRun> Runs { get; init; }

    public required IReadOnlyList<ComparedTag> Tags { get; init; }

    public required IReadOnlyList<VariantScenario> Scenarios { get; init; }

    public BaselineComparison? Baseline { get; init; }
}
