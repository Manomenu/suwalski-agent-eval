using System.Text.Json;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.Server.Evaluation.Invocation;

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
