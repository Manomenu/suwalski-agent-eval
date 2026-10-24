namespace Suwageval.Contracts.Runs;

/// <summary>A check's verdict in a run against its history.</summary>
public sealed record CheckRegression
{
    public required RegressionState State { get; init; }

    /// <summary>For <see cref="RegressionState.AlreadyBroken"/>: the first reference run of the failures in a row.</summary>
    public Guid? FailingSince { get; init; }

    /// <summary>The newest run of the history in which the check passed, to compare with; none when it never passed there.</summary>
    public Guid? LastPassedRun { get; init; }

    /// <summary>
    /// True when this run was on other retrieval data than <see cref="LastPassedRun"/> (their <c>dataVersion</c>s differ): a failure may come
    /// from the data, not the agent. False without a run that passed.
    /// </summary>
    public bool DataChanged { get; init; }

    /// <summary>The verdicts the state was computed from, oldest first: the newest reference runs of the check's history.</summary>
    public IReadOnlyList<CheckHistoryPoint> History { get; init; } = [];
}
