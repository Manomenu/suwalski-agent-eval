namespace Suwageval.Server.Simulation;

/// <summary>What the simulated user did at the start of a turn: wrote <see cref="Message"/>, ended the conversation, or could not be played.</summary>
internal sealed record SimulatedTurn
{
    public string? Message { get; private init; }

    public bool Ended { get; private init; }

    /// <summary>Why the user could not be played (the model is unreachable, too slow, or answered something unreadable); null otherwise.</summary>
    public string? Failure { get; private init; }

    public static SimulatedTurn Wrote(string message) => new() { Message = message };

    public static SimulatedTurn End() => new() { Ended = true };

    public static SimulatedTurn Failed(string failure) => new() { Failure = failure };
}
