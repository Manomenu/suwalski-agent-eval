using System.Text.Json;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.Server.Evaluation.Invocation;

/// <summary>
/// What goes with a turn besides the conversation: the scenario's fixtures and the conversation's id (sent in the body), and where
/// the call comes from (sent in the <c>baggage</c> header, for the agent's traces and logs — it never changes behaviour).
/// <paramref name="AgentVersionId"/> is the version Suwageval registered the run under (its id on Suwageval's pages).
/// <paramref name="ConversationId"/> is one per sample: a plain scenario's single turn is the first turn of its id, a simulation's turns share it.
/// </summary>
internal sealed record TurnContext(
    Guid RunId,
    string ScenarioKey,
    int Sample,
    ScenarioType ScenarioType,
    IReadOnlyList<string> ScenarioTags,
    IReadOnlyList<string> RunTags,
    string? RunGroup,
    string ConversationId,
    JsonElement? Fixtures = null,
    Guid? AgentVersionId = null
);
