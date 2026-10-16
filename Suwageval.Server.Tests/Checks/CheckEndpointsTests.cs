using System.Net;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Checks;

public sealed class CheckEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private async Task<string> AgentWithScenariosAsync(params string[] keys)
    {
        var agent = await server.NewAgent();
        var scenarios = keys.Select(key => new ScenarioInput { Key = key, Conversation = [new UserMessage { Text = "Hello" }] }).ToList();
        (await server.Put($"/agents/{agent}/scenarios", new PutScenariosRequest(scenarios))).EnsureSuccessStatusCode();
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
        var response = await server.Put($"/agents/{agent}/checks", new PutChecksRequest(checks));
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
                Contains = "refund",
                PassThreshold = 0.8,
            },
            new JudgeCheck
            {
                Key = "judge",
                ScenarioKey = "s",
                Criteria = "The assistant is polite.",
            },
        ];
        await Put(agent, checks);

        var listed = await TestServer.Read<CheckDetail[]>(await server.Get($"/agents/{agent}/checks"));

        Assert.Equal(
            [typeof(ToolCalledCheck), typeof(EventCheck), typeof(JudgeCheck), typeof(ToolNotCalledCheck), typeof(TextCheck)],
            listed.Select(detail => detail.Check.GetType())
        );
        Assert.Equal(0.8, Assert.IsType<TextCheck>(listed[4].Check).PassThreshold);
        Assert.Equal("The assistant is polite.", Assert.IsType<JudgeCheck>(listed[2].Check).Criteria);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_to_one_scenario()
    {
        var agent = await AgentWithScenariosAsync("one", "two");
        await Put(agent, Called("a", "one"), Called("b", "two"));

        var narrowed = await TestServer.Read<CheckDetail[]>(await server.Get($"/agents/{agent}/checks?scenario=two"));

        Assert.Equal(["b"], narrowed.Select(detail => detail.Check.Key));
    }

    [Fact]
    public async Task A_check_naming_an_unknown_scenario_is_a_422_and_nothing_of_the_request_is_written()
    {
        var agent = await AgentWithScenariosAsync("s");

        var response = await server.Put($"/agents/{agent}/checks", new PutChecksRequest([Called("good", "s"), Called("lost", "ghost")]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["checks[1].scenarioKey"], await TestServer.ErrorFields(response));
        Assert.Empty(await TestServer.Read<CheckDetail[]>(await server.Get($"/agents/{agent}/checks")));
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

        var response = await server.Put($"/agents/{agent}/checks", new PutChecksRequest([bad, empty, Called("empty", "s"), judge]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            ["checks[0].key", "checks[0].passThreshold", "checks[1].pattern", "checks[2].key", "checks[3].criteria"],
            await TestServer.ErrorFields(response)
        );
    }

    [Fact]
    public async Task A_text_check_with_nothing_to_look_for_is_a_422()
    {
        var agent = await AgentWithScenariosAsync("s");

        var response = await server.Put($"/agents/{agent}/checks", new PutChecksRequest([new TextCheck { Key = "t", ScenarioKey = "s" }]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["checks[0]"], await TestServer.ErrorFields(response));
    }

    [Fact]
    public async Task Deleting_a_check_is_a_204_then_a_404_and_an_unknown_agent_is_a_404()
    {
        var agent = await AgentWithScenariosAsync("s");
        await Put(agent, Called("a", "s"));

        var first = await server.Delete($"/agents/{agent}/checks/a");
        var second = await server.Delete($"/agents/{agent}/checks/a");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get("/agents/none/checks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Delete("/agents/none/checks/a")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Put("/agents/none/checks", new PutChecksRequest([]))).StatusCode);
    }
}
