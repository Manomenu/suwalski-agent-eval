using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Checks;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Checks;

public sealed class TraceTests
{
    private static UserMessage User(string text) => new() { Text = text };

    [Fact]
    public void Each_simulated_user_message_opens_a_turn_and_what_comes_before_the_first_is_the_agents_turn()
    {
        var trace = Trace.Of([Say("a1"), User("u2"), Call("get_order"), Say("a2"), User("u3"), Say("a3")]);

        Assert.Equal([1, 3, 2], trace.Turns.Select(turn => turn.Count));
        Assert.IsType<UserMessage>(trace.Turns[1][0]);
    }

    [Fact]
    public void A_simulation_the_user_opened_has_no_empty_turn_before_it() =>
        Assert.Equal([2, 2], Trace.Of([User("u1"), Say("a1"), User("u2"), Say("a2")]).Turns.Select(turn => turn.Count));

    [Fact]
    public void An_output_without_user_messages_is_one_turn_even_when_empty()
    {
        Assert.Single(Trace.Of([Call("get_order"), Say("done")]).Turns);
        Assert.Empty(Assert.Single(Trace.Of([]).Turns));
    }
}
