using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Scenarios;

public sealed record ScenarioInput
{
    public required string Key { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Fixtures { get; init; }

    public Continuation Continuation { get; init; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Samples { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AcGuardrail? AcGuardrail { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScenarioOrigin? Origin { get; init; }
}
