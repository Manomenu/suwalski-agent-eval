using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Checks;

/// <summary>
/// What the agent produced for one sample, turn by turn: the items one <c>POST /suwageval/turn</c> returned are one turn,
/// in the order the agent produced them. The first turn is number 1.
/// </summary>
internal sealed record Trace(IReadOnlyList<IReadOnlyList<ConversationItem>> Turns);
