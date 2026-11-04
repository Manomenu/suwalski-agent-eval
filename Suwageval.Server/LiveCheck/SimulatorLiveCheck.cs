using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Simulation;

namespace Suwageval.Server.LiveCheck;

internal static class SimulatorLiveCheck
{
    private const string User = "You want to know when the shop opens on Saturday. You write short messages in English.";

    private static readonly ConversationItem[] Conversation = [new AssistantMessage { Text = "Hello! How can I help you today?" }];

    public static async Task<string?> ProblemAsync(UserSimulator simulator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        if (!simulator.IsConfigured)
        {
            return null;
        }
        var turn = await simulator.NextAsync(User, Conversation, cancellationToken);
        return turn switch
        {
            { Failure: { } failure } => $"The simulated user could not be played (model entry '{simulator.ModelEntry}'): {failure}.",
            { Message: null } =>
                $"The simulated user (model entry '{simulator.ModelEntry}') ended a conversation in which they had not asked anything yet.",
            _ => null,
        };
    }
}
