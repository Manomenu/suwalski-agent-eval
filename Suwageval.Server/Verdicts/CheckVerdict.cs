using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Verdicts;

internal sealed record CheckVerdict(
    bool Passed,
    IReadOnlyList<ConversationItem> Evidence,
    string Message,
    bool NotJudged = false,
    string? ModelName = null
);
