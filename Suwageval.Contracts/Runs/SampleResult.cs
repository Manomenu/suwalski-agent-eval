using System.Text.Json.Serialization;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Runs;

public sealed record SampleResult
{
    public required int Sample { get; init; }

    public required SampleStatus Status { get; init; }

    public IReadOnlyList<ConversationItem>? Output { get; init; }

    public string? Error { get; init; }

    public int? DurationMs { get; init; }

    public IReadOnlyList<int>? TurnDurationsMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EndedByUser { get; init; }

    public IReadOnlyList<string> Undeclared { get; init; } = [];
}
