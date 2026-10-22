using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Simulation;

/// <summary>
/// The simulator's part of the daily check from production's network (<c>Suwageval.Server live-check</c>, Judges/JudgeLiveCheck.cs): one
/// call with a fixed tiny case through the production registration of the simulator. Its answer varies by design, so the check asks only
/// what must always hold: the user could be played, and wrote a message where a user plainly has something to say.
/// </summary>
internal static class SimulatorLiveCheck
{
    private const string User = "You want to know when the shop opens on Saturday. You write short messages in English.";

    private static readonly ConversationItem[] Conversation = [new AssistantMessage { Text = "Hello! How can I help you today?" }];

    /// <summary>What is wrong with the simulator right now, in a sentence, or null when it played the fixed case; null too when none is configured.</summary>
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
            { Failure: { } failure } => $"The simulated user could not be played: {failure}.",
            { Message: null } => "The simulated user ended a conversation in which they had not asked anything yet.",
            _ => null,
        };
    }
}
