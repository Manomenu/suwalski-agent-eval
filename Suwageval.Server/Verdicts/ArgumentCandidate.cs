using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Verdicts;

internal sealed record ArgumentCandidate(ConversationItem Item, IReadOnlyList<PendingJudgement> Judgements);
