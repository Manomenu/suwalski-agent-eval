namespace Suwageval.Contracts.Runs;

/// <summary>A scenario a re-check did not evaluate, and why: its stored answers answer a conversation that is not the current one.</summary>
public sealed record SkippedScenario
{
    public required string Key { get; init; }

    public required string Reason { get; init; }
}
