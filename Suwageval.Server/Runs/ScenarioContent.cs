using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Checks.Evaluation;

namespace Suwageval.Server.Runs;

/// <summary>The part of a stored scenario revision a run needs: what the agent is shown and how often.</summary>
internal sealed record ScenarioContent
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    /// <summary>Sent verbatim with every turn; null when the scenario has none.</summary>
    public JsonElement? Fixtures { get; init; }

    public Continuation Continuation { get; init; } = new();

    /// <summary>Not set (null): the agent's default, else 3.</summary>
    public int? Samples { get; init; }

    /// <summary>Whether the scenario is a simulation: a model plays the user and the conversation goes on for up to its turns.</summary>
    public bool Simulated => Continuation.SimulatedUser is not null;

    /// <summary>The trace of a sample's output: a simulation's turns, each opened by the simulated user's message; otherwise one turn.</summary>
    public Trace TraceOf(IReadOnlyList<ConversationItem> output) => Simulated ? Trace.Of(output) : new Trace([output]);
}
