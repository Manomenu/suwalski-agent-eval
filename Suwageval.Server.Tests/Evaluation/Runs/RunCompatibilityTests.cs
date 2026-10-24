using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Evaluation.Runs;
using static Suwageval.Server.Tests.Evaluation.Checks.Items;

namespace Suwageval.Server.Tests.Evaluation.Runs;

public sealed class RunCompatibilityTests
{
    private static ScenarioContent Content(params ConversationItem[] conversation) => new() { Conversation = conversation };

    private static IReadOnlyList<string> Mismatches(
        IReadOnlyList<(string, ScenarioContent)> scenarios,
        IReadOnlyList<CheckInput> checks,
        AgentContract? version = null
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
    public void Fixtures_need_a_version_that_declares_a_fixtures_schema()
    {
        var withFixtures = new ScenarioContent
        {
            Conversation = [new UserMessage { Text = "hi" }],
            Fixtures = Json( /*lang=json,strict*/
                """{"pinned":[]}"""
            ),
        };

        var one = Assert.Single(Mismatches([("s", withFixtures)], []));

        Assert.Contains("scenario 's'", one, StringComparison.Ordinal);
        Assert.Contains("fixturesSchema", one, StringComparison.Ordinal);
        Assert.Empty(Mismatches([("s", withFixtures)], [], SeededAgent.ExampleVersionWithFixtures));
        Assert.Empty(Mismatches([("s", Content(new UserMessage { Text = "hi" }))], []));
    }

    [Fact]
    public void An_event_not_emitted_check_must_name_a_declared_event()
    {
        var undeclared = new EventNotEmittedCheck
        {
            Key = "c",
            ScenarioKey = "s",
            Event = "nope",
        };
        var declared = undeclared with { Key = "d", Event = "state.changed" };

        var one = Assert.Single(Mismatches([], [undeclared, declared]));

        Assert.Contains("check 'c'", one, StringComparison.Ordinal);
        Assert.Contains("event 'nope'", one, StringComparison.Ordinal);
    }

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
                new AgentContract
                {
                    Agents =
                    [
                        new LlmAgentDefinition
                        {
                            Name = "a",
                            Model = new ModelDefinition { Name = "m" },
                            Config = "p",
                        },
                    ],
                    Context = new ContextDefinition
                    {
                        UsesRetrieval = true,
                        Retrieval = new RetrievalDefinition { DataVersion = "test-data" },
                    },
                }
            )
        );
    }

    [Fact]
    public void Where_after_and_before_must_name_a_tool_or_event_of_the_version()
    {
        var fits = new TextCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Contains = "x",
            Where = new CheckWhere { After = "get_order", Before = "state.changed" },
        };
        var nope = fits with
        {
            Key = "b",
            Where = new CheckWhere { After = "nope", Before = "state.changed" },
        };

        var one = Assert.Single(Mismatches([], [fits, nope]));

        Assert.Contains("check 'b'", one, StringComparison.Ordinal);
        Assert.Contains("where.after names 'nope'", one, StringComparison.Ordinal);
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

    private static AgentContract TwoAgents() =>
        SeededAgent.ExampleVersion with
        {
            Agents =
            [
                SeededAgent.ExampleVersion.Agents[0] with
                {
                    Tools = [SeededAgent.ExampleVersion.Agents[0].Tools[0]],
                },
                SeededAgent.ExampleVersion.Agents[0] with
                {
                    Name = "billing",
                    Tools = [SeededAgent.ExampleVersion.Agents[0].Tools[1]],
                },
            ],
        };

    [Fact]
    public void A_tool_without_an_agent_may_belong_to_any_agent() =>
        Assert.Empty(Mismatches([("s", Content(Call("get_order"), Call("refund_order")))], [], TwoAgents()));

    [Fact]
    public void A_tool_call_that_names_an_agent_needs_that_agent_to_exist_and_have_the_tool()
    {
        var mismatches = Mismatches(
            [
                (
                    "s",
                    Content(Call("get_order", agent: "billing"), Call("get_order", agent: "ghost"), Call("refund_order", agent: "billing"))
                ),
            ],
            [],
            TwoAgents()
        );

        Assert.Equal(2, mismatches.Count);
        Assert.Contains("agent 'billing' does not have", mismatches[0], StringComparison.Ordinal);
        Assert.Contains("agent 'ghost', which the version does not have", mismatches[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_check_by_an_agent_needs_that_agent_to_have_the_tool()
    {
        var mismatches = Mismatches(
            [("s", Content())],
            [
                new ToolCalledCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Tool = "get_order",
                    By = "support-agent",
                },
                new ToolCalledCheck
                {
                    Key = "b",
                    ScenarioKey = "s",
                    Tool = "get_order",
                    By = "billing",
                },
                new ToolNotCalledCheck
                {
                    Key = "c",
                    ScenarioKey = "s",
                    Tool = "get_order",
                    By = "ghost",
                },
            ],
            TwoAgents()
        );

        Assert.Equal(2, mismatches.Count);
        Assert.Contains("check 'b'", mismatches[0], StringComparison.Ordinal);
        Assert.Contains("check 'c'", mismatches[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_check_of_a_simulated_scenario_that_picks_a_turn_by_number_is_a_mismatch()
    {
        var simulation = new ScenarioContent
        {
            Conversation = [new UserMessage { Text = "hi" }],
            Continuation = new Continuation { Turns = 3, SimulatedUser = "Anna" },
        };
        var check = SeededAgent.RefundAfterLookup("c1", "s") with
        {
            Where = new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Number, 2) },
        };

        Assert.StartsWith(
            "check 'c1': where.turn is 'any' or 'last'",
            Assert.Single(Mismatches([("s", simulation)], [check])),
            StringComparison.Ordinal
        );
    }
}
