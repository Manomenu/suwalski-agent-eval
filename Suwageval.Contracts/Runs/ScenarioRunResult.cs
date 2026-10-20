namespace Suwageval.Contracts.Runs;

/// <summary>One scenario in a run: its samples and, per check, how many of them passed.</summary>
public sealed record ScenarioRunResult
{
    public required string Key { get; init; }

    /// <summary>The revision of the scenario that ran.</summary>
    public required int Revision { get; init; }

    /// <summary>
    /// The scenario as a scenario file would hold it (docs/scenario-format.md), written from what the server stores: the content of
    /// <see cref="Revision"/> and the checks in the revisions the run judged with. Its tags are the ones the scenario carries now: tags
    /// belong to no revision. Comments of the pushed file are not kept.
    /// </summary>
    public required string DefinitionYaml { get; init; }

    public required IReadOnlyList<SampleResult> Samples { get; init; }

    public required IReadOnlyList<CheckRunResult> Checks { get; init; }
}
