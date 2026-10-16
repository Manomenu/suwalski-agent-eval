namespace Suwageval.Contracts.Runs;

/// <summary>A run with what every scenario produced and how every check judged it.</summary>
public sealed record RunResults
{
    public required RunSummary Run { get; init; }

    public required IReadOnlyList<ScenarioRunResult> Scenarios { get; init; }
}
