namespace Suwageval.Contracts.Runs;

/// <summary>A compared run: its summary, its judge and its turn times, the latter against the baseline's.</summary>
public sealed record ComparedRun
{
    public required RunSummary Run { get; init; }

    /// <summary>The model that judged the run: the most common known model among its model-judged checks; absent when none was judged by a known model.</summary>
    public string? JudgeModel { get; init; }

    /// <summary>The judge and the baseline's are both known and differ: verdicts may differ because of the judge, not the agent.</summary>
    public required bool JudgeChanged { get; init; }

    /// <summary>Checks passed over checks (from 0 to 1); absent for a run without checks.</summary>
    public double? PassRate { get; init; }

    public double? MedianMs { get; init; }

    public double? P95Ms { get; init; }

    /// <summary>Absent for the baseline itself and when either run made no call.</summary>
    public TimeChange? MedianChange { get; init; }

    public TimeChange? P95Change { get; init; }
}
