namespace Suwageval.Contracts.Runs;

/// <summary>One scenario in a run: its samples and, per check, how many of them passed.</summary>
public sealed record ScenarioRunResult
{
    public required string Key { get; init; }

    /// <summary>The revision of the scenario that ran.</summary>
    public required int Revision { get; init; }

    public required IReadOnlyList<SampleResult> Samples { get; init; }

    public required IReadOnlyList<CheckRunResult> Checks { get; init; }
}
