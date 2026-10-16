using System.Net;
using Suwageval.Contracts;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Scenarios;

public sealed class ScenarioEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private static ScenarioInput Scenario(string key, string text = "Hello", params string[] tags) =>
        new()
        {
            Key = key,
            Tags = tags,
            Conversation = [new UserMessage { Text = text }],
        };

    private async Task<PutScenariosResponse> Put(string agent, params ScenarioInput[] scenarios)
    {
        var response = await server.Put($"/agents/{agent}/scenarios", new PutScenariosRequest(scenarios));
        response.EnsureSuccessStatusCode();
        return await TestServer.Read<PutScenariosResponse>(response);
    }

    [Fact]
    public async Task Putting_scenarios_reports_created_then_unchanged_then_updated()
    {
        var agent = await server.NewAgent();

        var first = await Put(agent, Scenario("a"), Scenario("b"));
        var again = await Put(agent, Scenario("a"), Scenario("b"));
        var changed = await Put(agent, Scenario("a", "Hi there"), Scenario("b"));

        Assert.Equal([new ScenarioResult("a", UpsertOutcome.Created), new ScenarioResult("b", UpsertOutcome.Created)], first.Results);
        Assert.All(again.Results, result => Assert.Equal(UpsertOutcome.Unchanged, result.Outcome));
        Assert.Equal([UpsertOutcome.Updated, UpsertOutcome.Unchanged], changed.Results.Select(result => result.Outcome));
    }

    [Fact]
    public async Task The_outcome_is_written_by_name_in_camel_case()
    {
        var agent = await server.NewAgent();

        var response = await server.Put($"/agents/{agent}/scenarios", new PutScenariosRequest([Scenario("a")]));

        Assert.Contains("\"outcome\":\"created\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_scenario_is_read_with_its_conversation_and_its_revisions_are_listed()
    {
        var agent = await server.NewAgent();
        await Put(agent, Scenario("a", "one", "x"));
        await Put(agent, Scenario("a", "two", "x"));

        var detail = await TestServer.Read<ScenarioDetail>(await server.Get($"/agents/{agent}/scenarios/a"));
        var revisions = await TestServer.Read<ScenarioRevision[]>(await server.Get($"/agents/{agent}/scenarios/a/revisions"));

        Assert.Equal(2, detail.Revision);
        Assert.Equal(["x"], detail.Scenario.Tags);
        Assert.Equal("two", Assert.IsType<UserMessage>(Assert.Single(detail.Scenario.Conversation)).Text);
        Assert.Equal([1, 2], revisions.Select(revision => revision.Revision));
        Assert.Equal("one", Assert.IsType<UserMessage>(Assert.Single(revisions[0].Scenario.Conversation)).Text);
        Assert.Empty(revisions[0].Scenario.Tags);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_by_tag()
    {
        var agent = await server.NewAgent();
        await Put(agent, Scenario("a", "Hello", "regression"), Scenario("b"));

        var all = await TestServer.Read<ScenarioSummary[]>(await server.Get($"/agents/{agent}/scenarios"));
        var tagged = await TestServer.Read<ScenarioSummary[]>(await server.Get($"/agents/{agent}/scenarios?tag=regression"));

        Assert.Equal(["a", "b"], all.Select(scenario => scenario.Key));
        Assert.Equal(["a"], tagged.Select(scenario => scenario.Key));
    }

    [Fact]
    public async Task Deleting_a_scenario_is_a_204_then_a_404()
    {
        var agent = await server.NewAgent();
        await Put(agent, Scenario("a"));

        var first = await server.Delete($"/agents/{agent}/scenarios/a");
        var second = await server.Delete($"/agents/{agent}/scenarios/a");
        var read = await server.Get($"/agents/{agent}/scenarios/a");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task An_unknown_scenario_or_agent_is_a_404()
    {
        var agent = await server.NewAgent();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Get($"/agents/{agent}/scenarios/none")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get($"/agents/{agent}/scenarios/none/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get("/agents/none/scenarios")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Put("/agents/none/scenarios", new PutScenariosRequest([]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get("/agents/none/tags")).StatusCode);
    }

    [Fact]
    public async Task Invalid_scenarios_are_a_422_naming_the_fields_and_nothing_is_written()
    {
        var agent = await server.NewAgent();
        var broken = Scenario("Not Kebab", "Hello", "Bad Tag") with { Samples = 0, Conversation = [] };

        var response = await server.Put(
            $"/agents/{agent}/scenarios",
            new PutScenariosRequest([Scenario("fine"), broken, Scenario("fine")])
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            ["scenarios[1].key", "scenarios[1].tags[0]", "scenarios[1].samples", "scenarios[1].conversation", "scenarios[2].key"],
            await TestServer.ErrorFields(response)
        );
        Assert.Empty(await TestServer.Read<ScenarioSummary[]>(await server.Get($"/agents/{agent}/scenarios")));
    }

    [Fact]
    public async Task Tags_change_on_several_scenarios_at_once_without_a_new_revision()
    {
        var agent = await server.NewAgent();
        await Put(agent, Scenario("a", "Hello", "old"), Scenario("b", "Hello", "old", "keep"));

        var response = await server.Post($"/agents/{agent}/scenarios/tags", new TagChangeRequest(["a", "b"], ["new"], ["old"]));
        var changed = await TestServer.Read<TagChangeResponse>(response);
        var tags = await TestServer.Read<TagUsage[]>(await server.Get($"/agents/{agent}/tags"));
        var detail = await TestServer.Read<ScenarioDetail>(await server.Get($"/agents/{agent}/scenarios/a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["a", "b"], changed.Scenarios.Select(scenario => scenario.Key));
        Assert.Equal(["new"], changed.Scenarios[0].Tags);
        Assert.Equal(["keep", "new"], changed.Scenarios[1].Tags);
        Assert.Equal([new TagUsage("keep", 1), new TagUsage("new", 2)], tags);
        Assert.Equal(1, detail.Revision);
    }

    [Fact]
    public async Task Changing_tags_of_unknown_scenarios_is_a_422_listing_them_and_changes_nothing()
    {
        var agent = await server.NewAgent();
        await Put(agent, Scenario("a"));

        var response = await server.Post($"/agents/{agent}/scenarios/tags", new TagChangeRequest(["a", "ghost"], ["new"], []));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ghost", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await TestServer.Read<TagUsage[]>(await server.Get($"/agents/{agent}/tags")));
    }

    [Fact]
    public async Task A_bad_tag_in_a_tag_change_is_a_422()
    {
        var agent = await server.NewAgent();

        var response = await server.Post($"/agents/{agent}/scenarios/tags", new TagChangeRequest(["a"], ["Bad Tag"], ["Also Bad"]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["add[0]", "remove[0]"], await TestServer.ErrorFields(response));
    }
}
