using Suwageval.Server.Agents;
using Suwageval.Server.Database;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Agents;

namespace Suwageval.Server.Tests.Scenarios;

public sealed class ScenarioStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ScenarioStore Store() => new(database.Pool);

    private async Task<Guid> NewAgentAsync() =>
        (await new AgentStore(database.Pool).CreateAsync(AgentStoreTests.UniqueName(), "http://agent.test", Cancellation)).Agent.Id;

    private static ScenarioItem Item(
        string key,
        string content = /*lang=json,strict*/
            """{"conversation":[]}""",
        params string[] tags
    ) => new(key, tags, content);

    [Fact]
    public async Task Upsert_creates_then_leaves_identical_content_unchanged()
    {
        var store = Store();
        var agent = await NewAgentAsync();

        var created = await store.UpsertAsync(agent, [Item("a"), Item("b")], Cancellation);
        var again = await store.UpsertAsync(agent, [Item("a"), Item("b")], Cancellation);

        Assert.Equal([ScenarioOutcome.Created, ScenarioOutcome.Created], created.Select(result => result.Outcome));
        Assert.Equal(["a", "b"], created.Select(result => result.Key));
        Assert.Equal([ScenarioOutcome.Unchanged, ScenarioOutcome.Unchanged], again.Select(result => result.Outcome));
        Assert.Equal([1], (await store.ListRevisionsAsync(agent, "a", Cancellation)).Select(revision => revision.Revision));
    }

    [Fact]
    public async Task Changed_content_makes_a_new_revision_and_the_latest_is_listed()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(
            agent,
            [
                Item(
                    "a", /*lang=json,strict*/
                    """{"v":1}"""
                ),
            ],
            Cancellation
        );

        var result = await store.UpsertAsync(
            agent,
            [
                Item(
                    "a", /*lang=json,strict*/
                    """{"v":2}"""
                ),
            ],
            Cancellation
        );

        Assert.Equal(ScenarioOutcome.Updated, Assert.Single(result).Outcome);
        var revisions = await store.ListRevisionsAsync(agent, "a", Cancellation);
        Assert.Equal([1, 2], revisions.Select(revision => revision.Revision));
        Assert.Equal(
            ContentHash.Of( /*lang=json,strict*/
                """{"v":2}"""
            ),
            revisions[1].ContentHash
        );
        var latest = Assert.Single(await store.ListAsync(agent, cancellationToken: Cancellation));
        Assert.Equal(2, latest.Revision);
        Assert.Contains("2", latest.Content, StringComparison.Ordinal);
        Assert.Equal(latest, await store.GetAsync(agent, "a", Cancellation));
    }

    [Fact]
    public async Task Tags_alone_change_without_a_new_revision()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(
            agent,
            [
                Item(
                    "a", /*lang=json,strict*/
                    """{"v":1}""",
                    "x"
                ),
            ],
            Cancellation
        );

        var result = await store.UpsertAsync(
            agent,
            [
                Item(
                    "a", /*lang=json,strict*/
                    """{"v":1}""",
                    "y",
                    "x"
                ),
            ],
            Cancellation
        );

        Assert.Equal(ScenarioOutcome.Unchanged, Assert.Single(result).Outcome);
        Assert.Single(await store.ListRevisionsAsync(agent, "a", Cancellation));
        Assert.Equal(["x", "y"], (await store.GetAsync(agent, "a", Cancellation))!.Tags);
    }

    [Fact]
    public async Task Keys_missing_from_the_list_are_untouched()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(agent, [Item("a"), Item("b")], Cancellation);

        await store.UpsertAsync(
            agent,
            [
                Item(
                    "a", /*lang=json,strict*/
                    """{"v":2}"""
                ),
            ],
            Cancellation
        );

        Assert.Equal(["a", "b"], (await store.ListAsync(agent, cancellationToken: Cancellation)).Select(row => row.Key));
        Assert.Single(await store.ListRevisionsAsync(agent, "b", Cancellation));
    }

    [Fact]
    public async Task Delete_hides_the_scenario_keeps_its_revisions_and_a_returning_key_is_undeleted_with_a_new_revision()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(agent, [Item("a")], Cancellation);

        Assert.True(await store.DeleteAsync(agent, "a", Cancellation));
        Assert.False(await store.DeleteAsync(agent, "a", Cancellation));
        Assert.False(await store.DeleteAsync(agent, "nope", Cancellation));
        Assert.Empty(await store.ListAsync(agent, cancellationToken: Cancellation));
        Assert.Null(await store.GetAsync(agent, "a", Cancellation));
        Assert.Single(await store.ListRevisionsAsync(agent, "a", Cancellation));

        var back = await store.UpsertAsync(agent, [Item("a")], Cancellation);

        Assert.Equal(ScenarioOutcome.Updated, Assert.Single(back).Outcome);
        Assert.Equal([1, 2], (await store.ListRevisionsAsync(agent, "a", Cancellation)).Select(revision => revision.Revision));
        Assert.Equal(2, (await store.GetAsync(agent, "a", Cancellation))!.Revision);
    }

    [Fact]
    public async Task List_filters_by_tag()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(agent, [Item("a", "{}", "smoke"), Item("b", "{}", "smoke", "slow"), Item("c", "{}")], Cancellation);

        Assert.Equal(["a", "b"], (await store.ListAsync(agent, "smoke", Cancellation)).Select(row => row.Key));
        Assert.Equal(["b"], (await store.ListAsync(agent, "slow", Cancellation)).Select(row => row.Key));
        Assert.Empty(await store.ListAsync(agent, "none", Cancellation));
        Assert.Equal(3, (await store.ListAsync(agent, cancellationToken: Cancellation)).Count);
    }

    [Fact]
    public async Task Scenarios_of_another_agent_are_invisible()
    {
        var store = Store();
        var one = await NewAgentAsync();
        var two = await NewAgentAsync();

        await store.UpsertAsync(one, [Item("a")], Cancellation);

        Assert.Empty(await store.ListAsync(two, cancellationToken: Cancellation));
        Assert.Null(await store.GetAsync(two, "a", Cancellation));
        Assert.Equal(ScenarioOutcome.Created, Assert.Single(await store.UpsertAsync(two, [Item("a")], Cancellation)).Outcome);
    }

    [Fact]
    public async Task Change_tags_adds_and_removes_reports_unknown_keys_and_makes_no_revision()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(agent, [Item("a", "{}", "old", "keep"), Item("b", "{}"), Item("gone", "{}")], Cancellation);
        await store.DeleteAsync(agent, "gone", Cancellation);

        var change = await store.ChangeTagsAsync(agent, ["a", "b", "missing", "gone"], ["new"], ["old"], Cancellation);

        Assert.Equal(["a", "b"], change.Changed.Select(scenario => scenario.Key));
        Assert.Equal(["keep", "new"], change.Changed[0].Tags);
        Assert.Equal(["new"], change.Changed[1].Tags);
        Assert.Equal(["missing", "gone"], change.UnknownKeys);
        Assert.Single(await store.ListRevisionsAsync(agent, "a", Cancellation));
    }

    [Fact]
    public async Task List_tags_counts_live_scenarios_per_tag()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await store.UpsertAsync(agent, [Item("a", "{}", "x", "y"), Item("b", "{}", "x"), Item("c", "{}", "z")], Cancellation);
        await store.DeleteAsync(agent, "c", Cancellation);

        var tags = await store.ListTagsAsync(agent, Cancellation);

        Assert.Equal([new TagCount("x", 2), new TagCount("y", 1)], tags);
    }
}
