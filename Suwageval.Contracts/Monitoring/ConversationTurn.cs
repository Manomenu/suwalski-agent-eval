using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Monitoring;

/// <summary>One turn of a monitored conversation as the agent recorded it.</summary>
public sealed record ConversationTurn
{
    public required Guid Id { get; init; }

    /// <summary>When the turn ended, by the agent's clock.</summary>
    public required DateTimeOffset At { get; init; }

    public required Guid AgentVersionId { get; init; }

    public string? DataVersion { get; init; }

    public required IReadOnlyList<ConversationItem> Items { get; init; }
}
