using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.Server.Tests.Invocation;

public sealed record PinnedFixtures(IReadOnlyList<string>? PinnedResources);

public sealed class TurnRequestTests
{
    private static readonly JsonElement NoArguments = JsonSerializer.SerializeToElement(new { });

    private static UserMessage User(string text) => new() { Text = text };

    private static AssistantMessage Assistant(string text) => new() { Text = text };

    private static ToolCall Call(string id) =>
        new()
        {
            Id = id,
            Name = "lookup",
            Arguments = NoArguments,
        };

    private static ToolResult Result(string callId) => new() { CallId = callId, Content = "found" };

    private static Event Changed() => new() { Type = "state.changed", Data = NoArguments };

    private static TurnRequest Request(params ConversationItem[] conversation) => new() { Conversation = conversation };

    [Fact]
    public void History_is_split_into_turns_with_results_paired_to_their_calls_and_events_kept_in_order()
    {
        var turns = Request(
                User("one"),
                Call("a"),
                Result("a"),
                Changed(),
                Assistant("first"),
                User("two"),
                Assistant("second"),
                User("current")
            )
            .HistoryTurns();

        Assert.Equal(2, turns.Count);
        var first = turns[0];
        Assert.Equal("one", first.User.Text);
        Assert.Equal("first", first.Assistant!.Text);
        Assert.Equal("ToolCall,ToolResult,Event", string.Join(",", first.Items.Select(item => item.GetType().Name)));
        var exchange = Assert.Single(first.ToolExchanges);
        Assert.Equal(("a", "a"), (exchange.Call.Id, exchange.Result.CallId));
        Assert.Empty(turns[1].Items);
    }

    [Fact]
    public void A_user_message_the_agent_never_answered_is_a_turn_without_an_assistant_message()
    {
        var turns = Request(User("one"), User("two"), User("current")).HistoryTurns();

        Assert.All(turns, turn => Assert.Null(turn.Assistant));
    }

    [Fact]
    public void A_tool_call_id_used_twice_is_rejected()
    {
        var request = Request(User("one"), Call("a"), Result("a"), Call("a"), Assistant("x"), User("current"));

        Assert.Contains("'a' appears twice", Assert.Throws<TurnRejectedException>(request.HistoryTurns).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tool_result_without_a_call_before_it_is_rejected()
    {
        var request = Request(User("one"), Result("a"), Assistant("x"), User("current"));

        Assert.Contains(
            "'a' has no tool call",
            Assert.Throws<TurnRejectedException>(request.HistoryTurns).Message,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tool_calls_not_closed_by_an_assistant_message_are_rejected(bool endsTheHistory)
    {
        ConversationItem[] unclosed = endsTheHistory
            ? [User("one"), Call("a"), Result("a"), User("current")]
            : [User("one"), Call("a"), Result("a"), User("two"), User("current")];

        Assert.Contains(
            "followed by the assistant message",
            Assert.Throws<TurnRejectedException>(Request(unclosed).HistoryTurns).Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Fixtures_are_read_as_the_type_and_are_null_when_the_scenario_has_none()
    {
        var withFixtures = new TurnRequest
        {
            Conversation = [User("hi")],
            Fixtures = JsonSerializer.SerializeToElement(new { pinnedResources = new List<string> { "r1" } }),
        };

        Assert.Equal("r1", Assert.Single(withFixtures.FixturesAs<PinnedFixtures>()!.PinnedResources!));
        Assert.Null(Request(User("hi")).FixturesAs<PinnedFixtures>());
    }

    [Fact]
    public void Fixtures_with_a_member_the_type_does_not_declare_are_rejected_with_a_readable_reason()
    {
        var request = new TurnRequest
        {
            Conversation = [User("hi")],
            Fixtures = JsonSerializer.SerializeToElement(new { pinnedResource = new List<string> { "r1" } }),
        };

        Assert.StartsWith(
            "The fixtures do not match the declared schema",
            Assert.Throws<TurnRejectedException>(request.FixturesAs<PinnedFixtures>).Message,
            StringComparison.Ordinal
        );
    }
}
