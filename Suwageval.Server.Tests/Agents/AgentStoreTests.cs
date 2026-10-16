using Suwageval.Server.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Tests.Agents;

public sealed class AgentStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private AgentStore Store() => new(database.Pool);

    internal static string UniqueName() => $"agent-{Guid.NewGuid():N}";

    [Fact]
    public async Task Create_stores_the_agent_and_it_is_found_by_name_and_by_id()
    {
        var store = Store();
        var name = UniqueName();

        var creation = await store.CreateAsync(name, "http://agent.test/run", Cancellation);

        Assert.True(creation.Created);
        Assert.Equal(name, creation.Agent.Name);
        Assert.Equal("http://agent.test/run", creation.Agent.EndpointUrl);
        Assert.Equal(creation.Agent, await store.GetAsync(name, Cancellation));
        Assert.Equal(creation.Agent, await store.GetAsync(creation.Agent.Id.ToString(), Cancellation));
        Assert.Contains(creation.Agent, await store.ListAsync(Cancellation));
    }

    [Fact]
    public async Task Create_with_a_taken_name_returns_the_existing_agent_and_changes_nothing()
    {
        var store = Store();
        var name = UniqueName();
        var first = await store.CreateAsync(name, "http://first.test", Cancellation);

        var second = await store.CreateAsync(name, "http://second.test", Cancellation);

        Assert.False(second.Created);
        Assert.Equal(first.Agent, second.Agent);
    }

    [Fact]
    public async Task Get_of_an_unknown_agent_is_null()
    {
        Assert.Null(await Store().GetAsync(UniqueName(), Cancellation));
        Assert.Null(await Store().GetAsync(Guid.NewGuid().ToString(), Cancellation));
    }

    [Fact]
    public async Task Registering_the_same_content_again_returns_the_existing_version()
    {
        var store = Store();
        var agent = (await store.CreateAsync(UniqueName(), "http://agent.test", Cancellation)).Agent;
        const string content = /*lang=json,strict*/
            """{"prompt":"be brief","model":"m1"}""";

        var first = await store.RegisterVersionAsync(agent.Id, content, "v1", Cancellation);
        var again = await store.RegisterVersionAsync(agent.Id, content, "other label", Cancellation);

        Assert.True(first.Created);
        Assert.False(again.Created);
        Assert.Equal(first.Version, again.Version);
        Assert.Equal("v1", again.Version.Label);
        Assert.Equal(ContentHash.Of(content), first.Version.ContentHash);
    }

    [Fact]
    public async Task Different_content_makes_a_new_version_and_versions_are_listed_newest_first()
    {
        var store = Store();
        var agent = (await store.CreateAsync(UniqueName(), "http://agent.test", Cancellation)).Agent;
        var first = await store.RegisterVersionAsync(
            agent.Id, /*lang=json,strict*/
            """{"model":"m1"}""",
            null,
            Cancellation
        );
        var second = await store.RegisterVersionAsync(
            agent.Id, /*lang=json,strict*/
            """{"model":"m2"}""",
            "second",
            Cancellation
        );

        var versions = await store.ListVersionsAsync(agent.Id, Cancellation);

        Assert.True(second.Created);
        Assert.Equal([second.Version.Id, first.Version.Id], versions.Select(version => version.Id));
        Assert.Equal(second.Version, await store.GetVersionAsync(second.Version.Id, Cancellation));
        Assert.Contains("m2", second.Version.Content, StringComparison.Ordinal);
        Assert.Null(first.Version.Label);
    }

    [Fact]
    public async Task The_same_content_is_a_separate_version_for_another_agent()
    {
        var store = Store();
        var one = (await store.CreateAsync(UniqueName(), "http://one.test", Cancellation)).Agent;
        var two = (await store.CreateAsync(UniqueName(), "http://two.test", Cancellation)).Agent;

        await store.RegisterVersionAsync(
            one.Id, /*lang=json,strict*/
            """{"model":"m1"}""",
            null,
            Cancellation
        );
        var registration = await store.RegisterVersionAsync(
            two.Id, /*lang=json,strict*/
            """{"model":"m1"}""",
            null,
            Cancellation
        );

        Assert.True(registration.Created);
        Assert.Single(await store.ListVersionsAsync(two.Id, Cancellation));
    }

    [Fact]
    public async Task Archived_versions_are_not_counted()
    {
        var store = Store();
        var agent = (await store.CreateAsync(UniqueName(), "http://one.test", Cancellation)).Agent;
        var first = await store.RegisterVersionAsync(
            agent.Id, /*lang=json,strict*/
            """{"model":"m1"}""",
            null,
            Cancellation
        );
        await store.RegisterVersionAsync(
            agent.Id, /*lang=json,strict*/
            """{"model":"m2"}""",
            null,
            Cancellation
        );

        await store.ArchiveVersionsAsync(agent.Id, [first.Version.Id], archived: true, Cancellation);

        Assert.Equal(1, (await store.CountAsync(agent.Id, Cancellation)).Versions);
    }

    [Fact]
    public async Task Registering_the_content_of_an_archived_version_unarchives_it()
    {
        var store = Store();
        var agent = (await store.CreateAsync(UniqueName(), "http://one.test", Cancellation)).Agent;
        var first = await store.RegisterVersionAsync(
            agent.Id, /*lang=json,strict*/
            """{"model":"m1"}""",
            null,
            Cancellation
        );
        await store.ArchiveVersionsAsync(agent.Id, [first.Version.Id], archived: true, Cancellation);
        Assert.NotNull((await store.GetVersionAsync(first.Version.Id, Cancellation))?.ArchivedAt);

        var again = await store.RegisterVersionAsync(
            agent.Id, /*lang=json,strict*/
            """{"model":"m1"}""",
            null,
            Cancellation
        );

        Assert.False(again.Created);
        Assert.Equal(first.Version.Id, again.Version.Id);
        Assert.Null(again.Version.ArchivedAt);
        Assert.Null((await store.GetVersionAsync(first.Version.Id, Cancellation))?.ArchivedAt);
    }

    [Fact]
    public async Task Archiving_keeps_the_first_time_and_unknown_versions_are_reported()
    {
        var store = Store();
        var agent = (await store.CreateAsync(UniqueName(), "http://one.test", Cancellation)).Agent;
        var version = (
            await store.RegisterVersionAsync(
                agent.Id, /*lang=json,strict*/
                """{"model":"m1"}""",
                null,
                Cancellation
            )
        ).Version;
        await store.ArchiveVersionsAsync(agent.Id, [version.Id], archived: true, Cancellation);
        var firstTime = (await store.GetVersionAsync(version.Id, Cancellation))?.ArchivedAt;

        await store.ArchiveVersionsAsync(agent.Id, [version.Id], archived: true, Cancellation);
        var missing = Guid.NewGuid();

        Assert.Equal(firstTime, (await store.GetVersionAsync(version.Id, Cancellation))?.ArchivedAt);
        Assert.Equal([missing], await store.UnknownVersionsAsync(agent.Id, [version.Id, missing], Cancellation));
    }

    [Fact]
    public async Task Set_endpoint_url_changes_only_the_url_and_count_starts_at_zero()
    {
        var store = Store();
        var agent = (await store.CreateAsync(UniqueName(), "http://old.test", Cancellation)).Agent;

        var moved = await store.SetEndpointUrlAsync(agent.Id, "http://new.test", Cancellation);

        Assert.Equal(agent with { EndpointUrl = "http://new.test" }, moved);
        Assert.Equal(new AgentCounts(0, 0, 0), await store.CountAsync(agent.Id, Cancellation));
    }

    [Fact]
    public async Task Get_version_of_an_unknown_id_is_null() => Assert.Null(await Store().GetVersionAsync(Guid.NewGuid(), Cancellation));
}
