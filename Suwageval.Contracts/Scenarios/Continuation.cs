using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Scenarios;

/// <summary>How the conversation goes on after its last message.</summary>
public sealed record Continuation
{
    /// <summary>
    /// How many agent turns are produced, at most (one: the agent answers the last message). Above one needs
    /// <c>simulatedUser</c>, who writes the user's message that starts each further turn; that user may end the
    /// conversation earlier.
    /// </summary>
    public int Turns { get; init; } = 1;

    /// <summary>
    /// The user Suwageval plays with a model, in plain words: their goal, temperament, situation, what they know, the language they
    /// write in. Set means the scenario is a simulation; its checks look at the whole conversation, never at one turn by number.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SimulatedUser { get; init; }
}
