using Suwageval.Server.Agents;
using Suwageval.Server.Agents.Versions;

namespace Suwageval.Server.Tests.Agents.Versions;

public sealed class VersionStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Archiving_keeps_the_first_time_and_unknown_versions_are_reported()
    {
        var store = new AgentStore(database.Pool);
        var versions = new VersionStore(database.Pool);
        var agent = (await store.CreateAsync(AgentStoreTests.UniqueName(), "http://one.test", Cancellation)).Agent;
        var version = (
            await store.RegisterVersionAsync(
                agent.Id, /*lang=json,strict*/
                """{"model":"m1"}""",
                null,
                cancellationToken: Cancellation
            )
        ).Version;
        await versions.ArchiveVersionsAsync(agent.Id, [version.Id], archived: true, Cancellation);
        var firstTime = (await store.GetVersionAsync(version.Id, Cancellation))?.ArchivedAt;

        await versions.ArchiveVersionsAsync(agent.Id, [version.Id], archived: true, Cancellation);
        var missing = Guid.NewGuid();

        Assert.Equal(firstTime, (await store.GetVersionAsync(version.Id, Cancellation))?.ArchivedAt);
        Assert.Equal([missing], await versions.UnknownVersionsAsync(agent.Id, [version.Id, missing], Cancellation));
    }
}
