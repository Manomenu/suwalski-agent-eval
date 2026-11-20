using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Verdicts;

internal sealed record Trace(IReadOnlyList<IReadOnlyList<ConversationItem>> Turns)
{
    public static Trace Of(IReadOnlyList<ConversationItem> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var turns = new List<IReadOnlyList<ConversationItem>>();
        var turn = new List<ConversationItem>();
        foreach (var item in output)
        {
            if (item is UserMessage && turn.Count > 0)
            {
                turns.Add(turn);
                turn = [];
            }
            turn.Add(item);
        }
        if (turn.Count > 0 || turns.Count == 0)
        {
            turns.Add(turn);
        }
        return new Trace(turns);
    }
}
