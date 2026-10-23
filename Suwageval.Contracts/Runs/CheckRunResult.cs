using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Runs;

/// <summary>One check over the samples of its scenario: the pass rate against the threshold.</summary>
public sealed record CheckRunResult
{
    public required string Key { get; init; }

    /// <summary>The revision of the check that ran.</summary>
    public required int Revision { get; init; }

    /// <summary>The check's newest revision now; above <see cref="Revision"/> when the check changed since the run.</summary>
    public required int LatestRevision { get; init; }

    /// <summary>The check as it was stored in <see cref="Revision"/>: the definition the run used.</summary>
    public required CheckInput Definition { get; init; }

    /// <summary>Whether a model gives the verdict of the check: a <c>judge</c> check, or one with a <c>$judge</c> matcher.</summary>
    public required bool JudgedByModel { get; init; }

    /// <summary>The model that gave the verdicts of the samples: the most common name among them (the first in sample order on a tie); null for a deterministic check and for runs made before the name was recorded.</summary>
    public string? JudgeModel { get; init; }

    /// <summary>True when <see cref="PassRate"/> reaches <see cref="PassThreshold"/>.</summary>
    public required bool Passed { get; init; }

    /// <summary>The share of samples that passed, from 0 to 1.</summary>
    public required double PassRate { get; init; }

    public required double PassThreshold { get; init; }

    public required IReadOnlyList<CheckSampleResult> Samples { get; init; }

    /// <summary>The verdict against the check's history; none until the run is done.</summary>
    public CheckRegression? Regression { get; init; }

    /// <summary>
    /// Passed for good: the threshold was reached and every sample was judged (a sample not judged leaves the verdict open). What
    /// "Hide passed checks" and <c>results --failed-only</c> leave out; the server decides it, the web and the CLI only read it.
    /// </summary>
    public required bool PassedFully { get; init; }
}
