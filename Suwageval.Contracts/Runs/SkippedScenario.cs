namespace Suwageval.Contracts.Runs;

public sealed record SkippedScenario
{
    public required string Key { get; init; }

    public required string Reason { get; init; }
}
