namespace Suwageval.Contracts.Runs;

public sealed record RecheckPlan
{
    public required bool Possible { get; init; }

    public string? Reason { get; init; }

    public RecheckBlocker? Blocker { get; init; }

    public required IReadOnlyList<ChangedCheck> ChangedChecks { get; init; }

    public required int JudgedChecks { get; init; }

    public string? JudgeModel { get; init; }

    public required IReadOnlyList<SkippedScenario> SkippedScenarios { get; init; }

    public required IReadOnlyList<string> Mismatches { get; init; }
}
