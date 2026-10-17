namespace Suwageval.Contracts.Runs;

/// <summary>
/// What a re-check of a run would do, and whether it can start at all. A re-check evaluates the run's stored answers again with
/// the current checks, so it is only worth starting when some check changed since the run; the agent is not asked and its current
/// contract plays no role.
/// </summary>
public sealed record RecheckPlan
{
    public required bool Possible { get; init; }

    /// <summary>Why not, in one sentence for a person; null when <see cref="Possible"/>.</summary>
    public string? Reason { get; init; }

    /// <summary>What stops the re-check; null when <see cref="Possible"/>.</summary>
    public RecheckBlocker? Blocker { get; init; }

    /// <summary>The checks that would be evaluated again, by scenario and check key; the others keep the verdict of the run.</summary>
    public required IReadOnlyList<ChangedCheck> ChangedChecks { get; init; }

    /// <summary>How many of the checks that would be evaluated again call the judge's model.</summary>
    public required int JudgedChecks { get; init; }

    /// <summary>The name of the judge's model as configured now, the one that would judge <see cref="JudgedChecks"/>; null when none is configured.</summary>
    public string? JudgeModel { get; init; }

    /// <summary>The scenarios of the run that would be left out, with the reason.</summary>
    public required IReadOnlyList<SkippedScenario> SkippedScenarios { get; init; }

    /// <summary>What does not fit between the current checks and the version the run was under test; empty when it all fits.</summary>
    public required IReadOnlyList<string> Mismatches { get; init; }
}
