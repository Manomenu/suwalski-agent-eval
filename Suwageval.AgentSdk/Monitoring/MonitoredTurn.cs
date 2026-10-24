using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Monitoring;

/// <summary>
/// One turn of a conversation in a monitored environment: what the user said and everything the agent did and answered, in order
/// (the same items as a turn of an evaluation, <see cref="Invocation.TurnResponse.Items"/>, with the user's message first).
/// </summary>
public sealed record MonitoredTurn
{
    /// <summary>The turn's own id, made by the sender: a turn sent twice (a retry) is stored once.</summary>
    public required Guid Id { get; init; }

    /// <summary>The agent's id of the conversation; its turns are one conversation in Suwageval (per environment).</summary>
    public required string ConversationId { get; init; }

    /// <summary>When the turn ended, by the agent's clock.</summary>
    public required DateTimeOffset At { get; init; }

    public required IReadOnlyList<ConversationItem> Items { get; init; }

    /// <summary>
    /// The retrieval data the turn was answered from (<c>context.retrieval.dataVersion</c>), when the agent can say it cheaply; none
    /// otherwise. Not part of the version: Suwageval only marks where it changed.
    /// </summary>
    public string? DataVersion { get; init; }
}
