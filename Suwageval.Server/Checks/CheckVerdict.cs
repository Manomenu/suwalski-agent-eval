using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Checks;

/// <summary>
/// A check's judgement of a trace: the items that decided it (possibly none) and a sentence saying why. <paramref name="NotJudged"/>
/// is only ever true for a judge check whose model could not give a verdict; such a verdict is never passed.
/// </summary>
internal sealed record CheckVerdict(bool Passed, IReadOnlyList<ConversationItem> Evidence, string Message, bool NotJudged = false);
