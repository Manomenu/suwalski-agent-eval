using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Simulation;

namespace Suwageval.Server.LiveCheck;

internal static class DescriberLiveCheck
{
    private static readonly ConversationItem[] Conversation =
    [
        new UserMessage { Text = "When does the shop open on Saturday?" },
        new AssistantMessage { Text = "On Saturday we open at 10:00." },
        new UserMessage { Text = "Thanks, bye." },
    ];

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
