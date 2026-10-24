using Suwageval.Server.Agents;
using Suwageval.Server.Database;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Tests.Agents;

namespace Suwageval.Server.Tests.Evaluation.Checks;

public sealed class CheckStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private CheckStore Store() => new(database.Pool);

    private async Task<Guid> NewAgentWithScenariosAsync(params string[] scenarioKeys)
    {
        var agent = (await new AgentStore(database.Pool).CreateAsync(AgentStoreTests.UniqueName(), "http://agent.test", Cancellation))
            .Agent
            .Id;
        await new ScenarioStore(database.Pool).UpsertAsync(
            agent,
            [.. scenarioKeys.Select(key => new ScenarioItem(key, [], "{}"))],
            Cancellation
        );
        return agent;
    }

    private static CheckItem Item(
        string key,
        string scenarioKey,
        string content = /*lang=json,strict*/
            """{"type":"contains"}"""
    ) => new(key, scenarioKey, content);

    [Fact]
    public async Task Upsert_creates_then_leaves_identical_content_unchanged()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1");

        var created = await store.UpsertAsync(agent, [Item("c1", "s1")], Cancellation);
        var again = await store.UpsertAsync(agent, [Item("c1", "s1")], Cancellation);

        Assert.Equal(CheckOutcome.Created, Assert.Single(created).Outcome);
        Assert.Equal(CheckOutcome.Unchanged, Assert.Single(again).Outcome);
        var row = Assert.Single(await store.ListAsync(agent, cancellationToken: Cancellation));
        Assert.Equal(("c1", "s1", 1), (row.Key, row.ScenarioKey, row.Revision));
    }

    [Fact]
    public async Task Changed_content_makes_a_new_revision()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1");
        await store.UpsertAsync(
            agent,
            [
                Item(
                    "c1",
                    "s1", /*lang=json,strict*/
                    """{"v":1}"""
                ),
            ],
            Cancellation
        );

        var result = await store.UpsertAsync(
            agent,
            [
                Item(
                    "c1",
                    "s1", /*lang=json,strict*/
                    """{"v":2}"""
                ),
            ],
            Cancellation
        );

        Assert.Equal(CheckOutcome.Updated, Assert.Single(result).Outcome);
        var row = Assert.Single(await store.ListAsync(agent, cancellationToken: Cancellation));
        Assert.Equal(2, row.Revision);
        Assert.Equal(
            ContentHash.Of( /*lang=json,strict*/
                """{"v":2}"""
            ),
            row.ContentHash
        );
    }

    [Fact]
    public async Task Moving_a_check_to_another_scenario_is_an_update()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1", "s2");
        await store.UpsertAsync(agent, [Item("c1", "s1")], Cancellation);

        var result = await store.UpsertAsync(agent, [Item("c1", "s2")], Cancellation);

        Assert.Equal(CheckOutcome.Updated, Assert.Single(result).Outcome);
        Assert.Empty(await store.ListAsync(agent, "s1", Cancellation));
        Assert.Equal(["c1"], (await store.ListAsync(agent, "s2", Cancellation)).Select(row => row.Key));
    }

    [Fact]
    public async Task An_unknown_scenario_is_reported_and_writes_nothing_for_that_item()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1");

        var results = await store.UpsertAsync(agent, [Item("ok", "s1"), Item("bad", "missing")], Cancellation);

        Assert.Equal([CheckOutcome.Created, CheckOutcome.UnknownScenario], results.Select(result => result.Outcome));
        Assert.Equal(["ok"], (await store.ListAsync(agent, cancellationToken: Cancellation)).Select(row => row.Key));
    }

    [Fact]
    public async Task A_deleted_scenario_counts_as_unknown()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1");
        await new ScenarioStore(database.Pool).DeleteAsync(agent, "s1", Cancellation);

        var result = await store.UpsertAsync(agent, [Item("c1", "s1")], Cancellation);

        Assert.Equal(CheckOutcome.UnknownScenario, Assert.Single(result).Outcome);
    }

    [Fact]
    public async Task List_filters_by_scenario_and_keys_missing_from_an_upsert_are_untouched()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1", "s2");
        await store.UpsertAsync(agent, [Item("a", "s1"), Item("b", "s2")], Cancellation);
        await store.UpsertAsync(
            agent,
            [
                Item(
                    "a",
                    "s1", /*lang=json,strict*/
                    """{"v":2}"""
                ),
            ],
            Cancellation
        );

        Assert.Equal(["a", "b"], (await store.ListAsync(agent, cancellationToken: Cancellation)).Select(row => row.Key));
        Assert.Equal(["b"], (await store.ListAsync(agent, "s2", Cancellation)).Select(row => row.Key));
    }

    [Fact]
    public async Task Delete_hides_the_check_and_a_returning_key_is_undeleted_with_a_new_revision()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1");
        await store.UpsertAsync(agent, [Item("c1", "s1")], Cancellation);

        Assert.True(await store.DeleteAsync(agent, "c1", Cancellation));
        Assert.False(await store.DeleteAsync(agent, "c1", Cancellation));
        Assert.Empty(await store.ListAsync(agent, cancellationToken: Cancellation));

        var back = await store.UpsertAsync(agent, [Item("c1", "s1")], Cancellation);

        Assert.Equal(CheckOutcome.Updated, Assert.Single(back).Outcome);
        Assert.Equal(2, Assert.Single(await store.ListAsync(agent, cancellationToken: Cancellation)).Revision);
    }

    [Fact]
    public async Task A_failing_item_rolls_the_whole_upsert_back()
    {
        var store = Store();
        var agent = await NewAgentWithScenariosAsync("s1");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.UpsertAsync(agent, [Item("good", "s1"), Item("broken", "s1", "not json")], Cancellation)
        );

        Assert.Empty(await store.ListAsync(agent, cancellationToken: Cancellation));
    }
}
