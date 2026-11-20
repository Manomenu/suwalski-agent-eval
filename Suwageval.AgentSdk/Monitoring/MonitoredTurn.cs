using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Monitoring;

public sealed record MonitoredTurn
{
    public required Guid Id { get; init; }

    public required string ConversationId { get; init; }

    public required DateTimeOffset At { get; init; }

    public required IReadOnlyList<ConversationItem> Items { get; init; }

    public string? DataVersion { get; init; }
}
