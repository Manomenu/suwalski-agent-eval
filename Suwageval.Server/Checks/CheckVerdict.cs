using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Checks;

/// <summary>A check's judgement of a trace: the items that decided it (possibly none) and a sentence saying why.</summary>
internal sealed record CheckVerdict(bool Passed, IReadOnlyList<ConversationItem> Evidence, string Message);
