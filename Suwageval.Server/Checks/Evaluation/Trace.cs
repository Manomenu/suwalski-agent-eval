using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Checks.Evaluation;

/// <summary>
/// What the agent produced for one sample, turn by turn: the items one <c>POST /suwageval/turn</c> returned are one turn,
/// in the order the agent produced them. The first turn is number 1. In a simulation a turn opens with the simulated user's
/// <c>UserMessage</c>, unless the agent answered the scenario's own last message.
/// </summary>
internal sealed record Trace(IReadOnlyList<IReadOnlyList<ConversationItem>> Turns)
{
    /// <summary>
    /// The trace of a sample's stored output: the agent never produces a <c>UserMessage</c>, so each one (the simulated user's) opens a
    /// turn; what comes before the first is the turn the agent started. A single turn has none, so it stays one turn.
    /// </summary>
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
