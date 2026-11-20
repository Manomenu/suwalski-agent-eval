using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Monitoring;

public sealed record ConversationTurn
{
    public required Guid Id { get; init; }

    public required DateTimeOffset At { get; init; }

    public required Guid AgentVersionId { get; init; }

    public string? DataVersion { get; init; }

    public required IReadOnlyList<ConversationItem> Items { get; init; }
}
