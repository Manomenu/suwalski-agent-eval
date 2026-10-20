using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Checks;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Checks;

public sealed class CheckScopeTests
{
    private static readonly TurnSelector Last = new(TurnSelectorKind.Last);

    private static TurnSelector Number(int n) => new(TurnSelectorKind.Number, n);

    private static string[] Names(CheckScope scope) =>
        [
            .. scope.Items.Select(item =>
                item switch
                {
                    ToolCall call => call.Name,
                    Event emitted => "event:" + emitted.Type,
                    AssistantMessage message => "say:" + message.Text,
                    _ => item.GetType().Name,
                }
            ),
        ];

    private static readonly Trace TwoTurns = Turns(
        [Call("get_order"), Result("call-get_order"), Call("refund_order"), Emit("state.changed"), Say("done")],
        [Call("get_order"), Say("anything else?")]
    );

    [Fact]
    public void Without_where_everything_of_every_turn_is_in_scope_in_order()
    {
        var scope = CheckScope.Of(TwoTurns, null);

        Assert.Equal(
            ["get_order", "ToolResult", "refund_order", "event:state.changed", "say:done", "get_order", "say:anything else?"],
            Names(scope)
        );
        Assert.Equal("", scope.Description);
    }

    [Fact]
    public void A_default_where_is_the_same_as_none() =>
        Assert.Equal(Names(CheckScope.Of(TwoTurns, null)), Names(CheckScope.Of(TwoTurns, new CheckWhere())));

    [Fact]
    public void A_turn_number_selects_that_turn_counted_from_one()
    {
        Assert.Equal(
            ["get_order", "ToolResult", "refund_order", "event:state.changed", "say:done"],
            Names(CheckScope.Of(TwoTurns, new CheckWhere { Turn = Number(1) }))
        );
        Assert.Equal(["get_order", "say:anything else?"], Names(CheckScope.Of(TwoTurns, new CheckWhere { Turn = Number(2) })));
    }

    [Fact]
    public void Last_selects_the_last_turn() =>
        Assert.Equal(["get_order", "say:anything else?"], Names(CheckScope.Of(TwoTurns, new CheckWhere { Turn = Last })));

    [Fact]
    public void A_turn_that_was_not_produced_leaves_nothing_and_says_how_many_were()
    {
        var scope = CheckScope.Of(TwoTurns, new CheckWhere { Turn = Number(3) });

        Assert.Empty(scope.Items);
        Assert.Contains("the agent produced 2", scope.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Last_of_an_empty_trace_leaves_nothing() => Assert.Empty(CheckScope.Of(Turns(), new CheckWhere { Turn = Last }).Items);

    [Fact]
    public void After_keeps_what_follows_the_first_occurrence_and_not_the_anchor_itself()
    {
        var scope = CheckScope.Of(TwoTurns, new CheckWhere { After = "get_order" });

        Assert.Equal(["ToolResult", "refund_order", "event:state.changed", "say:done", "get_order", "say:anything else?"], Names(scope));
    }

    [Fact]
    public void After_can_name_an_event()
    {
        var scope = CheckScope.Of(TwoTurns, new CheckWhere { After = "state.changed" });

        Assert.Equal(["say:done", "get_order", "say:anything else?"], Names(scope));
    }

    [Fact]
    public void Before_keeps_what_precedes_the_last_occurrence_and_not_the_anchor_itself()
    {
        var scope = CheckScope.Of(TwoTurns, new CheckWhere { Before = "get_order" });

        Assert.Equal(["get_order", "ToolResult", "refund_order", "event:state.changed", "say:done"], Names(scope));
    }

    [Fact]
    public void Before_the_only_occurrence_keeps_what_came_first() =>
        Assert.Equal(
            ["get_order", "ToolResult"],
            Names(CheckScope.Of(TwoTurns, new CheckWhere { Turn = Number(1), Before = "refund_order" }))
        );

    [Fact]
    public void After_and_before_together_keep_what_lies_between()
    {
        var scope = CheckScope.Of(
            TwoTurns,
            new CheckWhere
            {
                Turn = Number(1),
                After = "get_order",
                Before = "state.changed",
            }
        );

        Assert.Equal(["ToolResult", "refund_order"], Names(scope));
    }

    [Fact]
    public void After_and_before_in_the_wrong_order_leave_nothing() =>
        Assert.Empty(
            CheckScope
                .Of(
                    TwoTurns,
                    new CheckWhere
                    {
                        Turn = Number(1),
                        After = "state.changed",
                        Before = "get_order",
                    }
                )
                .Items
        );

    [Fact]
    public void The_anchor_is_looked_for_inside_the_selected_turns_only()
    {
        var trace = Turns([Call("get_order")], [Call("refund_order")]);

        var scope = CheckScope.Of(trace, new CheckWhere { Turn = Number(2), After = "get_order" });

        Assert.Empty(scope.Items);
        Assert.Contains("which did not happen", scope.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void An_anchor_that_does_not_occur_leaves_nothing_for_after_and_for_before()
    {
        Assert.Empty(CheckScope.Of(TwoTurns, new CheckWhere { After = "nope" }).Items);
        Assert.Empty(CheckScope.Of(TwoTurns, new CheckWhere { Before = "nope" }).Items);
    }

    [Fact]
    public void A_tool_result_or_a_message_is_never_an_anchor() =>
        Assert.Empty(CheckScope.Of(TwoTurns, new CheckWhere { After = "done" }).Items);
}
