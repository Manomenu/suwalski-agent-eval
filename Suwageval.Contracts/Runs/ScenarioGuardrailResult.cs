namespace Suwageval.Contracts.Runs;

public sealed record ScenarioGuardrailResult
{
    public required string Id { get; init; }

    public int? RetryLimit { get; init; }

    public int? Attempts { get; init; }

    public bool? Passed { get; init; }
}
