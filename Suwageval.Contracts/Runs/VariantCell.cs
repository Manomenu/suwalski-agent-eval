namespace Suwageval.Contracts.Runs;

/// <summary>One run's result for one scenario: its checks, each passed by the share of its samples against its pass threshold.</summary>
public sealed record VariantCell
{
    public required int ChecksPassed { get; init; }

    public required int ChecksTotal { get; init; }

    /// <summary>The keys of the checks that missed their threshold.</summary>
    public required IReadOnlyList<string> FailedChecks { get; init; }

    /// <summary>Every check of the scenario in this run, in the order the run lists them.</summary>
    public required IReadOnlyList<VariantCheck> Checks { get; init; }

    public required int Samples { get; init; }

    /// <summary>The samples where the agent gave no answer.</summary>
    public required int SamplesFailed { get; init; }

    /// <summary>The samples where the agent gave no answer, with their errors.</summary>
    public required IReadOnlyList<VariantNoAnswer> NoAnswers { get; init; }
}
