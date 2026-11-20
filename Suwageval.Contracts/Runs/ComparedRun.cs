namespace Suwageval.Contracts.Runs;

public sealed record ComparedRun
{
    public required RunSummary Run { get; init; }

    public string? JudgeModel { get; init; }

    public required bool JudgeChanged { get; init; }

    public double? PassRate { get; init; }

    public double? MedianMs { get; init; }

    public double? P95Ms { get; init; }

    public TimeChange? MedianChange { get; init; }

    public TimeChange? P95Change { get; init; }
}
