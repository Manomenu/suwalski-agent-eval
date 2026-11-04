using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Verdicts;

internal sealed record ArgumentSearch(
    string Subject,
    string Context,
    IReadOnlyList<ConversationItem> Named,
    IReadOnlyList<ArgumentCandidate> Candidates,
    CheckVerdict WhenNoCandidate
);
