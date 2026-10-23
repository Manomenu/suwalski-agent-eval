using Suwageval.Server.Agents;
using Suwageval.Server.Agents.Versions;
using Suwageval.Server.Runs;

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

    /// <summary>The ids of the four versions <see cref="SeedGroupRunsAsync"/> makes.</summary>
    internal sealed record GroupedVersions(Guid AgentId, Guid GroupOnly, Guid Mixed, Guid RecheckOfGroup, Guid NoRuns);

    /// <summary>
    /// One agent with a version whose only run is in a group, one with a run in a group and one in none, one whose only run is a re-check
    /// of a run in a group, and one with no run.
    /// </summary>
    internal static async Task<(string Name, GroupedVersions Versions)> SeedGroupRunsAsync(TestDatabase database)
    {
        var store = new AgentStore(database.Pool);
        var runs = new RunStore(database.Pool);
        var name = AgentStoreTests.UniqueName();
        var agent = (await store.CreateAsync(name, "http://one.test", Cancellation)).Agent;
        async Task<Guid> RegisterAsync(string model)
        {
            return (
                await store.RegisterVersionAsync(
                    agent.Id,
                    ContractContent.Json(AgentEndpointsTests.Version(model)),
                    null,
                    cancellationToken: Cancellation
                )
            )
                .Version
                .Id;
        }

        var groupOnly = await RegisterAsync("group-only");
        var mixed = await RegisterAsync("mixed");
        var recheckOfGroup = await RegisterAsync("recheck");
        var noRuns = await RegisterAsync("no-runs");
        await runs.EnqueueAsync(agent.Id, groupOnly, [], "http://one.test", runGroup: "sweep-1", cancellationToken: Cancellation);
        await runs.EnqueueAsync(agent.Id, mixed, [], "http://one.test", runGroup: "sweep-1", cancellationToken: Cancellation);
        await runs.EnqueueAsync(agent.Id, mixed, [], "http://one.test", cancellationToken: Cancellation);
        var original = await runs.EnqueueAsync(
            agent.Id,
            recheckOfGroup,
            [],
            "http://one.test",
            runGroup: "sweep-1",
            cancellationToken: Cancellation
        );
        await runs.EnqueueAsync(agent.Id, recheckOfGroup, [], "http://one.test", recheckOf: original, cancellationToken: Cancellation);
        return (name, new GroupedVersions(agent.Id, groupOnly, mixed, recheckOfGroup, noRuns));
    }

    [Fact]
    public async Task A_version_is_group_runs_only_when_it_has_runs_and_every_one_is_in_a_group_a_recheck_counting_as_its_original()
    {
        var (_, seeded) = await SeedGroupRunsAsync(database);

        var groupRunsOnly = await new VersionStore(database.Pool).GroupRunsOnlyAsync(seeded.AgentId, Cancellation);

        Assert.Equal(new[] { seeded.GroupOnly, seeded.RecheckOfGroup }.Order(), groupRunsOnly.Order());
    }

    [Fact]
    public async Task A_run_outside_a_group_makes_a_group_runs_only_version_ordinary_again()
    {
        var (_, seeded) = await SeedGroupRunsAsync(database);
        await new RunStore(database.Pool).EnqueueAsync(
            seeded.AgentId,
            seeded.GroupOnly,
            [],
            "http://one.test",
            cancellationToken: Cancellation
        );

        var groupRunsOnly = await new VersionStore(database.Pool).GroupRunsOnlyAsync(seeded.AgentId, Cancellation);

        Assert.Equal([seeded.RecheckOfGroup], groupRunsOnly);
    }
}
