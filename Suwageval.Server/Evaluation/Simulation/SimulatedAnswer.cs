namespace Suwageval.Server.Evaluation.Simulation;

/// <summary>What the simulator's model must answer, as JSON: the user's next message, or the end of the conversation.</summary>
internal sealed record SimulatedAnswer(string? Message, bool End);
