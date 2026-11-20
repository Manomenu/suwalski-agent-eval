using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Monitoring;

internal sealed class NoConversationRecorder : IConversationRecorder
{
    public void Record(string conversationId, IReadOnlyList<ConversationItem> items, string? dataVersion = null) { }
}
