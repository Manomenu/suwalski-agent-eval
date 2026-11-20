using System.Diagnostics;
using System.Threading.Channels;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.AgentSdk.Monitoring;

internal sealed class ConversationRecorder(Channel<MonitoredTurn> queue, TimeProvider time) : IConversationRecorder
{
    public void Record(string conversationId, IReadOnlyList<ConversationItem> items, string? dataVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0 || EvaluationTurn.IsCurrent || Activity.Current?.GetBaggageItem(SuwagevalBaggage.RunId) is not null)
        {
            return;
        }
        queue.Writer.TryWrite(
            new MonitoredTurn
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                At = time.GetUtcNow(),
                Items = [.. items],
                DataVersion = dataVersion,
            }
        );
    }
}
