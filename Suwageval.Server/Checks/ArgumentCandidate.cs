using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Checks;

/// <summary>A tool call or event that passed the deterministic part of a check, and the <c>$judge</c> values a model still has to judge.</summary>
internal sealed record ArgumentCandidate(ConversationItem Item, IReadOnlyList<PendingJudgement> Judgements);
