namespace Suwageval.Contracts.Runs;

/// <summary>One check of a scenario in one run, for the detail of a compare row: its verdict and why the samples that failed it did.</summary>
public sealed record VariantCheck
{
    public required string Key { get; init; }

    /// <summary>True when <see cref="PassRate"/> reaches the check's pass threshold.</summary>
    public required bool Passed { get; init; }

    /// <summary>The share of samples that passed, from 0 to 1.</summary>
    public required double PassRate { get; init; }

    public required int Samples { get; init; }

    public required int SamplesPassed { get; init; }

    /// <summary>The samples that did not pass, with the message the run page shows for them.</summary>
    public required IReadOnlyList<VariantFailure> Failures { get; init; }
}
