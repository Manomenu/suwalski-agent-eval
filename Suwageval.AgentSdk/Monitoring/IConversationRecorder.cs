using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Monitoring;

public interface IConversationRecorder
{
    void Record(string conversationId, IReadOnlyList<ConversationItem> items, string? dataVersion = null);
}
