using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.ScenarioDrafts;

internal sealed record MessageCut(bool UserMessage, int Ordinal)
{
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

    public string Describe() => $"{(UserMessage ? "user" : "agent")} message {Ordinal}";
}
