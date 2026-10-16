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

    /// <summary>True when <see cref="PassRate"/> reaches <see cref="PassThreshold"/>.</summary>
    public required bool Passed { get; init; }

    /// <summary>The share of samples that passed, from 0 to 1.</summary>
    public required double PassRate { get; init; }

    public required double PassThreshold { get; init; }

    public required IReadOnlyList<CheckSampleResult> Samples { get; init; }
}
