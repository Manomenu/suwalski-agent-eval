using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Runs;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Runs;

public sealed class RunCompatibilityTests
{
    private static ScenarioContent Content(params ConversationItem[] conversation) => new() { Conversation = conversation };

    private static IReadOnlyList<string> Mismatches(
        IReadOnlyList<(string, ScenarioContent)> scenarios,
        IReadOnlyList<CheckInput> checks,
        AgentVersionInput? version = null
    ) => RunCompatibility.Mismatches(version ?? SeededAgent.ExampleVersion, scenarios, checks);

    [Fact]
    public void A_scenario_and_checks_that_fit_have_no_mismatches() =>
        Assert.Empty(
            Mismatches(
                [("s", Content(new UserMessage { Text = "hi" }, Call("get_order"), Result("call-get_order"), Emit("state.changed")))],
                [SeededAgent.RefundAfterLookup("c1", "s"), SeededAgent.RefundedEvent("c2", "s")]
            )
        );

    [Fact]
    public void A_tool_call_in_the_conversation_must_be_a_tool_of_the_version()
    {
        var mismatches = Mismatches([("s", Content(Call("cancel_order")))], []);

        var one = Assert.Single(mismatches);
        Assert.Contains("scenario 's'", one, StringComparison.Ordinal);
        Assert.Contains("cancel_order", one, StringComparison.Ordinal);
    }

    [Fact]
    public void An_event_in_the_conversation_must_be_declared()
    {
        var one = Assert.Single(Mismatches([("s", Content(Emit("nope")))], []));

        Assert.Contains("event 'nope'", one, StringComparison.Ordinal);
    }

    [Fact]
    public void A_retrieval_needs_a_version_that_uses_retrieval()
    {
        var retrieval = new Retrieval { Query = "q", Chunks = [] };

        Assert.Single(Mismatches([("s", Content(retrieval))], []));
        Assert.Empty(
            Mismatches(
                [("s", Content(retrieval))],
                [],
                new AgentVersionInput
                {
                    SystemPrompt = "p",
                    Model = new ModelDefinition { Name = "m" },
                    Context = new ContextDefinition { UsesRetrieval = true },
                }
            )
        );
    }

    [Fact]
    public void A_check_must_name_a_tool_or_event_of_the_version()
    {
        var mismatches = Mismatches(
            [("s", Content())],
            [
                new ToolCalledCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Tool = "nope",
                },
                new ToolNotCalledCheck
                {
                    Key = "b",
                    ScenarioKey = "s",
                    Tool = "nope",
                },
                new EventCheck
                {
                    Key = "c",
                    ScenarioKey = "s",
                    Event = "nope",
                },
                new TextCheck
                {
                    Key = "d",
                    ScenarioKey = "s",
                    Contains = "x",
                },
            ]
        );

        Assert.Equal(3, mismatches.Count);
        Assert.Contains("check 'a'", mismatches[0], StringComparison.Ordinal);
        Assert.Contains("check 'b'", mismatches[1], StringComparison.Ordinal);
        Assert.Contains("check 'c'", mismatches[2], StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_mismatch_is_listed_once()
    {
        var call = Call("nope");

        Assert.Single(Mismatches([("s", Content(call, call))], []));
    }
}
