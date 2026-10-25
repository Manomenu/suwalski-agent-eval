using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.ScenarioDrafts;

/// <summary>Where a copied conversation ends: at its <see cref="Ordinal"/>-th (from 1) user message, or agent message, included.</summary>
internal sealed record MessageCut(bool UserMessage, int Ordinal)
{
    /// <summary>The index of that message among the items; null when the conversation has fewer such messages.</summary>
    public int? IndexIn(IReadOnlyList<ConversationItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var seen = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if ((UserMessage ? items[index] is UserMessage : items[index] is AssistantMessage) && ++seen == Ordinal)
            {
                return index;
            }
        }
        return null;
    }

    /// <summary>"user message 2", "agent message 3".</summary>
    public string Describe() => $"{(UserMessage ? "user" : "agent")} message {Ordinal}";
}
