using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Checks.Evaluation;

/// <summary>
/// A <c>tool-called</c> / <c>event</c> check with <c>$judge</c> matchers, evaluated as far as no model is needed: the candidates (items in
/// scope with the right name whose literals matched and where every <c>$judge</c> path exists), in order. The model-free result the
/// <c>Judges</c> layer finishes. <paramref name="Subject"/> is the sentence that opens the verdict ("refund was called in the last
/// turn"), <paramref name="Context"/> names the item for the model ("tool call refund"), <paramref name="Named"/> is every item
/// with the check's name (the evidence of a failure) and <paramref name="WhenNoCandidate"/> the verdict when there is no candidate.
/// </summary>
internal sealed record ArgumentSearch(
    string Subject,
    string Context,
    IReadOnlyList<ConversationItem> Named,
    IReadOnlyList<ArgumentCandidate> Candidates,
    CheckVerdict WhenNoCandidate
);
