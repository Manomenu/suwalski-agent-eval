using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Monitoring;

/// <summary>
/// Where a monitored agent records each turn of its real conversations, after answering it. Recording never waits for the network and
/// never fails the turn: the turns go to Suwageval from a queue in the background (<see cref="SuwagevalMonitoring"/>). A turn of an
/// evaluation (<c>POST /suwageval/turn</c>, or with Suwageval's run in the baggage) is not recorded.
/// </summary>
public interface IConversationRecorder
{
    /// <param name="conversationId">The agent's id of the conversation: the turns with one id are one conversation.</param>
    /// <param name="items">The turn: the user's message first, then what the agent did and answered, in order.</param>
    /// <param name="dataVersion">The retrieval data the answer came from, when the agent can say it cheaply.</param>
    void Record(string conversationId, IReadOnlyList<ConversationItem> items, string? dataVersion = null);
}
