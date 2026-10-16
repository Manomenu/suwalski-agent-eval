namespace Suwageval.Contracts.Runs;

/// <summary>One check over the samples of its scenario: the pass rate against the threshold.</summary>
public sealed record CheckRunResult
{
    public required string Key { get; init; }

    /// <summary>The revision of the check that ran.</summary>
    public required int Revision { get; init; }

    /// <summary>True when <see cref="PassRate"/> reaches <see cref="PassThreshold"/>.</summary>
    public required bool Passed { get; init; }

    /// <summary>The share of samples that passed, from 0 to 1.</summary>
    public required double PassRate { get; init; }

    public required double PassThreshold { get; init; }

    public required IReadOnlyList<CheckSampleResult> Samples { get; init; }
}
