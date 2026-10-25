using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Simulation;

namespace Suwageval.Server.LiveCheck;

/// <summary>
/// The user describer's part of the daily check from production's network (<c>Suwageval.Server live-check</c>, LiveCheck/JudgeLiveCheck.cs):
/// one call with a fixed tiny conversation, only when the describer has a model of its own (<c>Models:describer</c>); without one it asks
/// the judge's model, which the check already asks. Its wording varies, so the check asks only that the model wrote the description.
/// </summary>
internal static class DescriberLiveCheck
{
    private static readonly ConversationItem[] Conversation =
    [
        new UserMessage { Text = "When does the shop open on Saturday?" },
        new AssistantMessage { Text = "On Saturday we open at 10:00." },
        new UserMessage { Text = "Thanks, bye." },
    ];

    /// <summary>What is wrong with the describer right now, in a sentence, or null when it described the fixed case; null too without a model of its own.</summary>
    public static async Task<string?> ProblemAsync(UserDescriber describer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(describer);
        if (!describer.HasOwnModel)
        {
            return null;
        }
        var description = await describer.DescribeAsync(Conversation, cancellationToken);
        return description.Notice is { } notice ? $"The user describer (model entry '{describer.ModelEntry}') failed: {notice}" : null;
    }
}
