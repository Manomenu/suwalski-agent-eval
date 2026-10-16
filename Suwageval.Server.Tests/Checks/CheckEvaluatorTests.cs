using Suwageval.Contracts.Checks;
using Suwageval.Server.Checks;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Checks;

public sealed class CheckEvaluatorTests
{
    private static CheckVerdict Run(CheckInput check, Trace trace) => CheckEvaluator.Evaluate(check, trace);

    private static ToolCalledCheck Called(string tool, string? args = null, CheckWhere? where = null) =>
        new()
        {
            Key = "c",
            ScenarioKey = "s",
            Tool = tool,
            Args = args is null ? null : Json(args),
            Where = where,
        };

    private static TextCheck TextOf(
        string? contains = null,
        string? notContains = null,
        string? pattern = null,
        CheckWhere? where = null
    ) =>
        new()
        {
            Key = "c",
            ScenarioKey = "s",
            Contains = contains,
            NotContains = notContains,
            Pattern = pattern,
            Where = where,
        };

    [Fact]
    public void Tool_called_passes_with_the_call_as_evidence()
    {
        var call = Call(
            "get_order", /*lang=json,strict*/
            """{"order_id":"A-17"}"""
        );

        var verdict = Run(Called("get_order"), Turn(call, Say("hi")));

        Assert.True(verdict.Passed);
        Assert.Equal([call], verdict.Evidence);
    }

    [Fact]
    public void Tool_called_fails_when_the_tool_was_not_called()
    {
        var verdict = Run(Called("refund_order"), Turn(Call("get_order")));

        Assert.False(verdict.Passed);
        Assert.Empty(verdict.Evidence);
        Assert.Contains("refund_order was not called", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_called_matches_args_as_a_subset()
    {
        var call = Call(
            "refund_order", /*lang=json,strict*/
            """{"order_id":"A-17","reason":"broken"}"""
        );

        Assert.True(
            Run(
                Called(
                    "refund_order", /*lang=json,strict*/
                    """{"order_id":"A-17"}"""
                ),
                Turn(call)
            ).Passed
        );
    }

    [Fact]
    public void Tool_called_with_other_args_fails_and_shows_the_near_miss()
    {
        var call = Call(
            "refund_order", /*lang=json,strict*/
            """{"order_id":"A-18"}"""
        );

        var verdict = Run(
            Called(
                "refund_order", /*lang=json,strict*/
                """{"order_id":"A-17"}"""
            ),
            Turn(call)
        );

        Assert.False(verdict.Passed);
        Assert.Equal([call], verdict.Evidence);
        Assert.Contains("but not with arguments", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_called_passes_when_any_of_several_calls_matches()
    {
        var wrong = Call(
            "get_order", /*lang=json,strict*/
            """{"order_id":"A-1"}""",
            "1"
        );
        var right = Call(
            "get_order", /*lang=json,strict*/
            """{"order_id":"A-17"}""",
            "2"
        );

        var verdict = Run(
            Called(
                "get_order", /*lang=json,strict*/
                """{"order_id":"A-17"}"""
            ),
            Turn(wrong, right)
        );

        Assert.True(verdict.Passed);
        Assert.Equal([right], verdict.Evidence);
    }

    [Fact]
    public void Tool_called_honours_the_turn()
    {
        var trace = Turns([Call("get_order")], [Call("refund_order")]);
        static CheckWhere turn(int n)
        {
            return new() { Turn = new TurnSelector(TurnSelectorKind.Number, n) };
        }

        Assert.False(Run(Called("refund_order", where: turn(1)), trace).Passed);
        Assert.True(Run(Called("refund_order", where: turn(2)), trace).Passed);
        Assert.True(Run(Called("refund_order", where: new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Last) }), trace).Passed);
        Assert.True(Run(Called("refund_order"), trace).Passed);
    }

    [Fact]
    public void Tool_called_after_requires_the_anchor_to_come_first()
    {
        var after = new CheckWhere { After = "get_order" };

        Assert.True(Run(Called("refund_order", where: after), Turn(Call("get_order"), Call("refund_order"))).Passed);
        Assert.False(Run(Called("refund_order", where: after), Turn(Call("refund_order"), Call("get_order"))).Passed);
        Assert.False(Run(Called("refund_order", where: after), Turn(Call("refund_order"))).Passed);
    }

    [Fact]
    public void Tool_called_before_requires_the_anchor_to_come_later()
    {
        var before = new CheckWhere { Before = "refund_order" };

        Assert.True(Run(Called("get_order", where: before), Turn(Call("get_order"), Call("refund_order"))).Passed);
        Assert.False(Run(Called("get_order", where: before), Turn(Call("refund_order"), Call("get_order"))).Passed);
    }

    [Fact]
    public void Tool_not_called_passes_without_the_tool_and_fails_with_it()
    {
        var check = new ToolNotCalledCheck
        {
            Key = "c",
            ScenarioKey = "s",
            Tool = "refund_order",
        };
        var call = Call("refund_order");

        Assert.True(Run(check, Turn(Call("get_order"), Say("no"))).Passed);
        var failed = Run(check, Turn(call));
        Assert.False(failed.Passed);
        Assert.Equal([call], failed.Evidence);
    }

    [Fact]
    public void Tool_not_called_only_looks_at_its_scope()
    {
        var check = new ToolNotCalledCheck
        {
            Key = "c",
            ScenarioKey = "s",
            Tool = "refund_order",
            Where = new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Number, 1) },
        };

        Assert.True(Run(check, Turns([Call("get_order")], [Call("refund_order")])).Passed);
    }

    [Fact]
    public void Tool_not_called_after_an_anchor_that_never_happened_passes_because_nothing_follows()
    {
        var check = new ToolNotCalledCheck
        {
            Key = "c",
            ScenarioKey = "s",
            Tool = "refund_order",
            Where = new CheckWhere { After = "get_order" },
        };

        Assert.True(Run(check, Turn(Call("refund_order"))).Passed);
    }

    private static EventCheck EventOf(string type, string? data = null, CheckWhere? where = null) =>
        new()
        {
            Key = "c",
            ScenarioKey = "s",
            Event = type,
            Data = data is null ? null : Json(data),
            Where = where,
        };

    [Fact]
    public void Event_passes_on_the_type_and_on_a_data_subset()
    {
        var emitted = Emit(
            "state.changed", /*lang=json,strict*/
            """{"node":"refunded","orderId":"A-17"}"""
        );
        var trace = Turn(emitted);

        Assert.True(Run(EventOf("state.changed"), trace).Passed);
        var verdict = Run(
            EventOf(
                "state.changed", /*lang=json,strict*/
                """{"node":"refunded"}"""
            ),
            trace
        );
        Assert.True(verdict.Passed);
        Assert.Equal([emitted], verdict.Evidence);
    }

    [Fact]
    public void Event_fails_for_another_type_other_data_or_no_event()
    {
        var emitted = Emit(
            "state.changed", /*lang=json,strict*/
            """{"node":"open"}"""
        );

        Assert.False(Run(EventOf("other"), Turn(emitted)).Passed);
        var wrongData = Run(
            EventOf(
                "state.changed", /*lang=json,strict*/
                """{"node":"refunded"}"""
            ),
            Turn(emitted)
        );
        Assert.False(wrongData.Passed);
        Assert.Equal([emitted], wrongData.Evidence);
        Assert.False(Run(EventOf("state.changed"), Turn(Say("x"))).Passed);
    }

    [Fact]
    public void Event_after_a_tool_call_needs_the_call_first()
    {
        var where = new CheckWhere { After = "refund_order" };

        Assert.True(Run(EventOf("state.changed", where: where), Turn(Call("refund_order"), Emit("state.changed"))).Passed);
        Assert.False(Run(EventOf("state.changed", where: where), Turn(Emit("state.changed"), Call("refund_order"))).Passed);
    }

    [Fact]
    public void Text_contains_and_not_contains_look_at_the_answer_case_sensitively()
    {
        var trace = Turn(Call("get_order"), Say("Your refund is on its way."));

        Assert.True(Run(TextOf(contains: "refund"), trace).Passed);
        Assert.False(Run(TextOf(contains: "Refund"), trace).Passed);
        Assert.True(Run(TextOf(notContains: "sorry"), trace).Passed);
        var failed = Run(TextOf(notContains: "refund"), trace);
        Assert.False(failed.Passed);
        Assert.Contains("contains \"refund\"", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_pattern_is_a_regular_expression()
    {
        var trace = Turn(Say("Order A-17 refunded"));

        Assert.True(Run(TextOf(pattern: @"A-\d+ refunded"), trace).Passed);
        Assert.True(Run(TextOf(pattern: "(?i)REFUNDED"), trace).Passed);
        Assert.False(Run(TextOf(pattern: "^refunded"), trace).Passed);
    }

    [Fact]
    public void Text_conditions_all_have_to_hold()
    {
        var trace = Turn(Say("refund done"));

        Assert.True(Run(TextOf(contains: "refund", notContains: "sorry", pattern: "done$"), trace).Passed);
        Assert.False(Run(TextOf(contains: "refund", notContains: "done"), trace).Passed);
    }

    [Fact]
    public void Text_joins_the_messages_of_the_scope_and_ignores_other_items()
    {
        var first = Say("one");
        var second = Say("two");
        var trace = Turns([first, Call("get_order")], [second]);

        var all = Run(TextOf(pattern: "one\ntwo"), trace);
        var lastOnly = Run(TextOf(contains: "one", where: new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Last) }), trace);

        Assert.True(all.Passed);
        Assert.Equal([first, second], [.. all.Evidence]);
        Assert.False(lastOnly.Passed);
    }

    [Fact]
    public void Text_without_any_answer_fails_contains_and_passes_not_contains()
    {
        var trace = Turn(Call("get_order"));

        var contains = Run(TextOf(contains: "x"), trace);
        Assert.False(contains.Passed);
        Assert.Contains("There is no answer", contains.Message, StringComparison.Ordinal);
        Assert.True(Run(TextOf(notContains: "x"), trace).Passed);
    }

    [Fact]
    public void Text_after_a_tool_call_only_reads_the_messages_after_it()
    {
        var where = new CheckWhere { After = "get_order" };
        var trace = Turn(Say("looking"), Call("get_order"), Say("it is delivered"));

        Assert.True(Run(TextOf(contains: "delivered", where: where), trace).Passed);
        Assert.False(Run(TextOf(contains: "looking", where: where), trace).Passed);
    }

    [Fact]
    public void An_invalid_pattern_fails_the_check_instead_of_throwing()
    {
        var verdict = Run(TextOf(pattern: "("), Turn(Say("x")));

        Assert.False(verdict.Passed);
        Assert.Contains("invalid pattern", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_trace_fails_what_needs_something_and_passes_what_needs_nothing()
    {
        Assert.False(Run(Called("get_order"), Turns()).Passed);
        Assert.False(Run(EventOf("e"), Turns()).Passed);
        Assert.True(
            Run(
                new ToolNotCalledCheck
                {
                    Key = "c",
                    ScenarioKey = "s",
                    Tool = "t",
                },
                Turns()
            ).Passed
        );
    }
}
