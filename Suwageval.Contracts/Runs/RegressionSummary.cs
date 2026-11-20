namespace Suwageval.Contracts.Runs;

public sealed record RegressionSummary
{
    public string? ComparedWith { get; init; }

    public required int History { get; init; }

    public required int Regressions { get; init; }

    public required int New { get; init; }

    public required int AlreadyBroken { get; init; }

    public required int KnownFailures { get; init; }

    public required int Fixed { get; init; }

    public required int Unstable { get; init; }

    public required int AgentErrors { get; init; }
}
