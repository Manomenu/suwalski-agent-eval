namespace Suwageval.Contracts.Runs;

public sealed record RunResults
{
    public required RunSummary Run { get; init; }

    public required IReadOnlyList<ScenarioRunResult> Scenarios { get; init; }

    public double? MedianMs { get; init; }

    public double? P95Ms { get; init; }

    public GateResult? Gate { get; init; }

    public RegressionSummary? Regression { get; init; }
}
