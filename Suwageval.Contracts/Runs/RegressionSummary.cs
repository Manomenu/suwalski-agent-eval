namespace Suwageval.Contracts.Runs;

/// <summary>How a run's checks stand against their history: what the run is compared with and how many checks are in each state.</summary>
public sealed record RegressionSummary
{
    /// <summary>The branch whose reference runs make the history; none means every run of the agent (the run has no git origin).</summary>
    public string? ComparedWith { get; init; }

    /// <summary>How many reference runs make a check's history.</summary>
    public required int History { get; init; }

    public required int Regressions { get; init; }

    public required int New { get; init; }

    public required int AlreadyBroken { get; init; }

    public required int KnownFailures { get; init; }

    public required int Fixed { get; init; }

    public required int Unstable { get; init; }

    public required int AgentErrors { get; init; }
}
