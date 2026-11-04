using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Evaluation.Runs;

internal sealed record ScenarioContent
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    public JsonElement? Fixtures { get; init; }

    public Continuation Continuation { get; init; } = new();

    public int? Samples { get; init; }

    public AcGuardrail? AcGuardrail { get; init; }

    public bool Simulated => Continuation.SimulatedUser is not null;

    public Trace TraceOf(IReadOnlyList<ConversationItem> output) => Simulated ? Trace.Of(output) : new Trace([output]);
}
