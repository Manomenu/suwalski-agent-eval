namespace Suwageval.Contracts.Scenarios;

/// <summary>How the conversation goes on after its last message.</summary>
public sealed record Continuation
{
    /// <summary>How many agent turns are produced (one: the agent answers the last message).</summary>
    public int Turns { get; init; } = 1;
}
