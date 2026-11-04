using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Judges;

internal static class JudgeModel
{
    public static CheckVerdict NotJudged(IReadOnlyList<ConversationItem> evidence, string why, string? modelName = null) =>
        new(false, evidence, $"Not judged: {why}.", NotJudged: true, ModelName: modelName);
}
