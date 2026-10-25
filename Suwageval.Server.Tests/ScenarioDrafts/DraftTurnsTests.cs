using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.ScenarioDrafts;

namespace Suwageval.Server.Tests.ScenarioDrafts;

public sealed class DraftTurnsTests
{
    private static List<ConversationItem> Items() => [.. ScenarioDraftWriterTests.Turns().SelectMany(turn => turn)];

    [Theory]
    // Up to the first user message: the agent answers it, then the second user message opens one more turn.
    [InlineData(0, 2)]
    // Up to the first agent message: the second user message opens the only turn left.
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    // Up to the last message: nothing left to simulate.
    [InlineData(6, 0)]
    public void The_turns_left_are_those_the_real_conversation_had_after_the_cut(int end, int turns) =>
        Assert.Equal(turns, DraftTurns.After(Items(), end));

    [Fact]
    public void A_long_conversation_is_cut_to_the_most_turns_a_scenario_may_have()
    {
        List<ConversationItem> items = [.. Enumerable.Range(1, 30).Select(i => new UserMessage { Text = $"message {i}" })];

        Assert.Equal(20, DraftTurns.After(items, 0));
    }
}
