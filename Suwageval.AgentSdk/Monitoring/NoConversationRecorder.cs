using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Monitoring;

/// <summary>The recorder of an agent with no <c>Suwageval:Url</c>: monitoring is not set up, so a turn goes nowhere.</summary>
internal sealed class NoConversationRecorder : IConversationRecorder
{
    public void Record(string conversationId, IReadOnlyList<ConversationItem> items, string? dataVersion = null) { }
}
