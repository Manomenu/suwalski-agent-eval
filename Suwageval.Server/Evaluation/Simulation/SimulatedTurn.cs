namespace Suwageval.Server.Evaluation.Simulation;

internal sealed record SimulatedTurn
{
    public string? Message { get; private init; }

    public bool Ended { get; private init; }

    public string? Failure { get; private init; }

    public static SimulatedTurn Wrote(string message) => new() { Message = message };

    public static SimulatedTurn End() => new() { Ended = true };

    public static SimulatedTurn Failed(string failure) => new() { Failure = failure };
}
