using System.Net;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Evaluation.Checks;

public sealed class CheckEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private async Task<string> AgentWithScenariosAsync(params string[] keys)
    {
        var agent = await server.NewAgent();
        var scenarios = keys.Select(key => new ScenarioInput { Key = key, Conversation = [new UserMessage { Text = "Hello" }] }).ToList();
        (await server.Put($"/eval/agents/{agent}/scenarios", new PutScenariosRequest(scenarios))).EnsureSuccessStatusCode();
        return agent;
    }

    private static ToolCalledCheck Called(string key, string scenario, string tool = "get_order") =>
        new()
        {
            Key = key,
            ScenarioKey = scenario,
            Tool = tool,
        };

    private async Task<PutChecksResponse> Put(string agent, params CheckInput[] checks)
    {
        var response = await server.Put($"/eval/agents/{agent}/checks", new PutChecksRequest(checks));
        response.EnsureSuccessStatusCode();
        return await TestServer.Read<PutChecksResponse>(response);
    }

    [Fact]
    public async Task Putting_checks_reports_created_then_unchanged_then_updated()
    {
        var agent = await AgentWithScenariosAsync("s");

        var first = await Put(agent, Called("a", "s"), Called("b", "s"));
        var again = await Put(agent, Called("a", "s"), Called("b", "s"));
        var changed = await Put(agent, Called("a", "s", "refund_order"), Called("b", "s"));

        Assert.Equal([new CheckResult("a", UpsertOutcome.Created), new CheckResult("b", UpsertOutcome.Created)], first.Results);
        Assert.All(again.Results, result => Assert.Equal(UpsertOutcome.Unchanged, result.Outcome));
        Assert.Equal([UpsertOutcome.Updated, UpsertOutcome.Unchanged], changed.Results.Select(result => result.Outcome));
    }

    [Fact]
    public async Task A_check_left_out_of_the_put_is_deleted()
    {
        var agent = await AgentWithScenariosAsync("s");
        await Put(agent, Called("a", "s"), Called("b", "s"));

        var put = await Put(agent, Called("a", "s"));

        Assert.Equal([new CheckResult("a", UpsertOutcome.Unchanged), new CheckResult("b", UpsertOutcome.Deleted)], put.Results);
        var all = await TestServer.Read<CheckDetail[]>(await server.Get($"/eval/agents/{agent}/checks"));
        Assert.Equal(["a"], all.Select(check => check.Check.Key));
    }

    [Fact]
    public async Task Every_kind_of_check_comes_back_as_what_it_was()
    {
        var agent = await AgentWithScenariosAsync("s");
        CheckInput[] checks =
        [
            Called("called", "s"),
            new ToolNotCalledCheck
            {
                Key = "not-called",
                ScenarioKey = "s",
                Tool = "refund_order",
            },
            new EventCheck
            {
                Key = "event",
                ScenarioKey = "s",
                Event = "state.changed",
            },
            new TextCheck
            {
                Key = "text",
                ScenarioKey = "s",
                Contains = ["refund"],
                PassThreshold = 0.8,
            },
            new EventNotEmittedCheck
            {
                Key = "no-event",
                ScenarioKey = "s",
                Event = "procedure.started",
            },
            new CitedCheck
            {
                Key = "cited",
                ScenarioKey = "s",
                Sources = ["plans"],
            },
            Called("with-result", "s") with
            {
                Result = Items.Json("""{"status":{"$pattern":"deliv"}}"""),
            },
            new JudgeCheck
            {
                Key = "judge",
                ScenarioKey = "s",
                Criteria = "The assistant is polite.",
            },
        ];
        await Put(agent, checks);

        var listed = await TestServer.Read<CheckDetail[]>(await server.Get($"/eval/agents/{agent}/checks"));

        Assert.Equal(
            [
                typeof(ToolCalledCheck),
                typeof(CitedCheck),
                typeof(EventCheck),
                typeof(JudgeCheck),
                typeof(EventNotEmittedCheck),
                typeof(ToolNotCalledCheck),
                typeof(TextCheck),
                typeof(ToolCalledCheck),
            ],
            listed.Select(detail => detail.Check.GetType())
        );
        Assert.Equal(0.8, Assert.IsType<TextCheck>(listed[6].Check).PassThreshold);
        Assert.Equal("The assistant is polite.", Assert.IsType<JudgeCheck>(listed[3].Check).Criteria);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_to_one_scenario()
    {
        var agent = await AgentWithScenariosAsync("one", "two");
        await Put(agent, Called("a", "one"), Called("b", "two"));

        var narrowed = await TestServer.Read<CheckDetail[]>(await server.Get($"/eval/agents/{agent}/checks?scenario=two"));

        Assert.Equal(["b"], narrowed.Select(detail => detail.Check.Key));
    }

    [Fact]
    public async Task A_check_naming_an_unknown_scenario_is_a_422_and_nothing_of_the_request_is_written()
    {
        var agent = await AgentWithScenariosAsync("s");

        var response = await server.Put(
            $"/eval/agents/{agent}/checks",
            new PutChecksRequest([Called("good", "s"), Called("lost", "ghost")])
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["checks[1].scenarioKey"], await TestServer.ErrorFields(response));
        Assert.Empty(await TestServer.Read<CheckDetail[]>(await server.Get($"/eval/agents/{agent}/checks")));
    }

    [Fact]
    public async Task Invalid_checks_are_a_422_naming_the_fields()
    {
        var agent = await AgentWithScenariosAsync("s");
        var bad = Called("Bad Key", "s") with { PassThreshold = 0 };
        var empty = new TextCheck
        {
            Key = "empty",
            ScenarioKey = "s",
            Pattern = "(",
        };

        var judge = new JudgeCheck
        {
            Key = "judge",
            ScenarioKey = "s",
            Criteria = "  ",
        };

        var response = await server.Put($"/eval/agents/{agent}/checks", new PutChecksRequest([bad, empty, Called("empty", "s"), judge]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            ["checks[0].key", "checks[0].passThreshold", "checks[1].pattern", "checks[2].key", "checks[3].criteria"],
            await TestServer.ErrorFields(response)
        );
    }

    [Fact]
    public async Task Malformed_judge_matchers_are_a_422_naming_the_path_of_each()
    {
        var agent = await AgentWithScenariosAsync("s");
        var bad = new ToolCalledCheck
        {
            Key = "bad",
            ScenarioKey = "s",
            Tool = "get_order",
            Args = Items.Json(
                """
                {"a":{"$judge":{}},"b":{"$judge":{"description":" "}},"c":{"$judge":{"examples":[]}},
                 "d":{"$judge":{"description":"x","hint":1}},"e":{"$judge":{"description":"x"},"f":1},"g":[{"$judge":"text"}]}
                """
            ),
        };
        var badEvent = new EventCheck
        {
            Key = "bad-event",
            ScenarioKey = "s",
            Event = "state.changed",
            Data = Items.Json("""{"x":{"$judge":{"examples":"no"}}}"""),
        };

        var response = await server.Put($"/eval/agents/{agent}/checks", new PutChecksRequest([bad, badEvent]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            [
                "checks[0].args.a.$judge",
                "checks[0].args.b.$judge.description",
                "checks[0].args.c.$judge.examples",
                "checks[0].args.d.$judge",
                "checks[0].args.e",
                "checks[0].args.g[0].$judge",
                "checks[1].data.x.$judge.examples",
            ],
            await TestServer.ErrorFields(response)
        );
    }

    [Fact]
    public async Task A_correct_judge_matcher_is_stored_as_written()
    {
        var agent = await AgentWithScenariosAsync("s");
        var check = new ToolCalledCheck
        {
            Key = "reason-fits",
            ScenarioKey = "s",
            Tool = "get_order",
            Args = Items.Json("""{"reason":{"$judge":{"description":"Says it arrived damaged","examples":["kettle arrived broken"]}}}"""),
        };

        var response = await server.Put($"/eval/agents/{agent}/checks", new PutChecksRequest([check]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_text_check_with_nothing_to_look_for_is_a_422()
    {
        var agent = await AgentWithScenariosAsync("s");

        var response = await server.Put(
            $"/eval/agents/{agent}/checks",
            new PutChecksRequest([new TextCheck { Key = "t", ScenarioKey = "s" }])
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["checks[0]"], await TestServer.ErrorFields(response));
    }

    [Fact]
    public async Task Deleting_a_check_is_a_204_then_a_404_and_an_unknown_agent_is_a_404()
    {
        var agent = await AgentWithScenariosAsync("s");
        await Put(agent, Called("a", "s"));

        var first = await server.Delete($"/eval/agents/{agent}/checks/a");
        var second = await server.Delete($"/eval/agents/{agent}/checks/a");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get("/eval/agents/none/checks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Delete("/eval/agents/none/checks/a")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Put("/eval/agents/none/checks", new PutChecksRequest([]))).StatusCode);
    }

    [Fact]
    public async Task A_judge_in_a_result_is_checked_and_one_in_event_not_emitted_or_mixed_with_a_pattern_is_a_422()
    {
        var agent = await AgentWithScenariosAsync("s");
        var badResult = new ToolCalledCheck
        {
            Key = "bad-result",
            ScenarioKey = "s",
            Tool = "get_order",
            Result = Items.Json("""{"note":{"$judge":{}}}"""),
        };
        var mixed = new ToolCalledCheck
        {
            Key = "mixed",
            ScenarioKey = "s",
            Tool = "get_order",
            Args = Items.Json("""{"reason":{"$judge":{"description":"d"},"$pattern":"x"}}"""),
        };
        var inverted = new EventNotEmittedCheck
        {
            Key = "inverted",
            ScenarioKey = "s",
            Event = "state.changed",
            Data = Items.Json("""{"x":{"$judge":{"description":"d"}}}"""),
        };

        var response = await server.Put($"/eval/agents/{agent}/checks", new PutChecksRequest([badResult, mixed, inverted]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["checks[0].result.note.$judge", "checks[1].args.reason", "checks[2].data"], await TestServer.ErrorFields(response));
    }

    [Fact]
    [Trait("criterion", "AC-message-count-check-5")]
    public async Task A_message_count_check_is_stored_with_its_bounds_and_a_push_refuses_a_broken_one()
    {
        var agent = await AgentWithScenariosAsync("s");
        await Put(
            agent,
            new MessageCountCheck
            {
                Key = "only-one-reply",
                ScenarioKey = "s",
                Role = MessageRole.Assistant,
                Max = 1,
                Where = new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Last) },
            }
        );

        var listed = await TestServer.Read<CheckDetail[]>(await server.Get($"/eval/agents/{agent}/checks"));
        var noBound = new MessageCountCheck { Key = "no-bound", ScenarioKey = "s" };
        var refused = await server.Put($"/eval/agents/{agent}/checks", new PutChecksRequest([noBound]));

        var stored = Assert.IsType<MessageCountCheck>(Assert.Single(listed).Check);
        Assert.Equal((MessageRole.Assistant, 1, TurnSelectorKind.Last), (stored.Role, stored.Max, stored.Where?.Turn.Kind));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["checks[0]"], await TestServer.ErrorFields(refused));
    }

    [Fact]
    [Trait("criterion", "AC-check-options-6")]
    public async Task The_new_options_are_stored_and_a_push_refuses_a_broken_one()
    {
        var agent = await AgentWithScenariosAsync("s");
        await Put(
            agent,
            new EventCheck
            {
                Key = "started-once",
                ScenarioKey = "s",
                Event = "procedure.started",
                Max = 1,
            },
            new EventCheck
            {
                Key = "ended-known",
                ScenarioKey = "s",
                Event = "state.changed",
                Last = true,
                Data = Items.Json("""{"facts":{"age":60}}"""),
            },
            new TextCheck
            {
                Key = "no-technical-word",
                ScenarioKey = "s",
                NotContains = ["otherType", "quoteRequest"],
            }
        );

        var listed = (await TestServer.Read<CheckDetail[]>(await server.Get($"/eval/agents/{agent}/checks"))).ToDictionary(
            detail => detail.Check.Key,
            detail => detail.Check
        );
        var refused = await server.Put(
            $"/eval/agents/{agent}/checks",
            new PutChecksRequest([
                new EventCheck
                {
                    Key = "broken",
                    ScenarioKey = "s",
                    Event = "state.changed",
                    Last = true,
                },
            ])
        );

        Assert.Equal(1, Assert.IsType<EventCheck>(listed["started-once"]).Max);
        Assert.True(Assert.IsType<EventCheck>(listed["ended-known"]).Last);
        Assert.Equal(["otherType", "quoteRequest"], Assert.IsType<TextCheck>(listed["no-technical-word"]).NotContains);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["checks[0].last"], await TestServer.ErrorFields(refused));
    }

    [Fact]
    [Trait("criterion", "AC-check-options-6")]
    public async Task A_check_written_before_the_options_is_unchanged_when_pushed_again()
    {
        var agent = await AgentWithScenariosAsync("s");
        // Why: the body an older CLI sends, with one text as a plain string and no event bounds
        using var older = System.Text.Json.JsonDocument.Parse(
            """
            {"checks":[
              {"type":"text","key":"t","scenarioKey":"s","notContains":"secret"},
              {"type":"event","key":"e","scenarioKey":"s","event":"state.changed","data":{"node":"open"}}
            ]}
            """
        );
        (await server.Put($"/eval/agents/{agent}/checks", older.RootElement)).EnsureSuccessStatusCode();

        var again = await Put(
            agent,
            new TextCheck
            {
                Key = "t",
                ScenarioKey = "s",
                NotContains = ["secret"],
            },
            new EventCheck
            {
                Key = "e",
                ScenarioKey = "s",
                Event = "state.changed",
                Data = Items.Json("""{"node":"open"}"""),
            }
        );
        Assert.All(again.Results, result => Assert.Equal(UpsertOutcome.Unchanged, result.Outcome));
    }

    // Why: the forms the server stored before the options, read back from a real database; a check keeps its revision only while its JSON is these bytes
    [Theory]
    [Trait("criterion", "AC-check-options-6")]
    [InlineData(
        """{"type":"text","contains":null,"notContains":"secret","pattern":null,"key":"t","scenarioKey":"s","where":null,"passThreshold":1}"""
    )]
    [InlineData(
        """{"type":"text","contains":null,"notContains":null,"pattern":"^Hi","key":"t","scenarioKey":"s","where":null,"passThreshold":1}"""
    )]
    [InlineData(
        """{"type":"event","event":"procedure.started","data":{"procedure":"quoteRequest"},"key":"e","scenarioKey":"s","where":null,"passThreshold":1}"""
    )]
    [InlineData("""{"type":"event","event":"procedure.started","data":null,"key":"e","scenarioKey":"s","where":null,"passThreshold":1}""")]
    public void A_check_written_before_the_options_is_stored_in_the_same_bytes(string stored)
    {
        var check = System.Text.Json.JsonSerializer.Deserialize<CheckInput>(stored, Suwageval.AgentSdk.ContractJson.Options);

        Assert.Equal(stored, System.Text.Json.JsonSerializer.Serialize(check, Suwageval.AgentSdk.ContractJson.Options));
    }
}
