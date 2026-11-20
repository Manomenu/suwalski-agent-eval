namespace Suwageval.Contracts.Runs;

public sealed record ScenarioRunResult
{
    public required string Key { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required int Revision { get; init; }

    public required string DefinitionYaml { get; init; }

    public ScenarioGuardrailResult? AcGuardrail { get; init; }

    public required IReadOnlyList<SampleResult> Samples { get; init; }

    public required IReadOnlyList<CheckRunResult> Checks { get; init; }

    public required bool PassedFully { get; init; }
}
