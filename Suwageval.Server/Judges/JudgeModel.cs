using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Judges;

/// <summary>What the two judges — the <c>judge</c> check (<see cref="Judge"/>) and <c>$judge</c> matching (<see cref="ArgumentJudge"/>) — share: the "not judged" verdict.</summary>
internal static class JudgeModel
{
    /// <summary>The verdict of a check the model could not judge: failed, flagged, with the reason after the "Not judged:" prefix.</summary>
    public static CheckVerdict NotJudged(IReadOnlyList<ConversationItem> evidence, string why, string? modelName = null) =>
        new(false, evidence, $"Not judged: {why}.", NotJudged: true, ModelName: modelName);
}
