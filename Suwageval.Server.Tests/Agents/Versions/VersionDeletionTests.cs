using System.Net;
using Dapper;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Tests.Api;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests.Agents.Versions;

/// <summary>Deleting an archived version: the refusals, the preview and the cascade through runs, samples and verdicts — on a real PostgreSQL.</summary>
public sealed class VersionDeletionTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private async Task ArchiveAsync(SeededAgent agent, params Guid[] versions) =>
        (
            await server.Post($"/agents/{agent.Name}/versions/archive", new ArchiveVersionsRequest(versions, Archived: true))
        ).EnsureSuccessStatusCode();

    private async Task<(int Runs, int Samples, int Verdicts)> RowsOfAsync(params Guid[] runs)
    {
        await using var connection = await database.Pool.OpenAsync(Cancellation);
        return await connection.QuerySingleAsync<(int, int, int)>(
            """
            SELECT (SELECT count(*) FROM runs WHERE id = ANY(@runs))::int,
                   (SELECT count(*) FROM scenario_results WHERE run_id = ANY(@runs))::int,
                   (SELECT count(*) FROM check_results c JOIN scenario_results s ON s.id = c.scenario_result_id WHERE s.run_id = ANY(@runs))::int
            """,
            new { runs }
        );
    }

    private async Task<bool> VersionExistsAsync(Guid versionId)
    {
        await using var connection = await database.Pool.OpenAsync(Cancellation);
        return await connection.QuerySingleAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM agent_versions WHERE id = @versionId)",
            new { versionId }
        );
    }

    [Fact]
    public async Task Deleting_an_archived_version_removes_its_runs_samples_verdicts_and_rechecks_and_leaves_other_versions_alone()
    {
        var agent = await DeletionTests.SeedAsync(database);
        var other = await DeletionTests.OtherVersionAsync(database, agent);
        var first = await DeletionTests.FinishedRunAsync(database, agent, agent.VersionId, samples: 2);
        var recheck = await DeletionTests.FinishedRunAsync(database, agent, agent.VersionId, samples: 1, recheckOf: first);
        var kept = await DeletionTests.FinishedRunAsync(database, agent, other, samples: 3);
        await ArchiveAsync(agent, agent.VersionId);

        var preview = await server.Get($"/agents/{agent.Name}/versions/{agent.VersionId}/deletion");
        var deleted = await server.Delete($"/agents/{agent.Name}/versions/{agent.VersionId}");

        Assert.Equal(new VersionDeletion(agent.VersionId, 2, 3, 3), await TestServer.Read<VersionDeletion>(preview));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await VersionExistsAsync(agent.VersionId));
        Assert.Equal((0, 0, 0), await RowsOfAsync(first, recheck));
        Assert.True(await VersionExistsAsync(other));
        Assert.Equal((1, 3, 3), await RowsOfAsync(kept));
    }

    [Fact]
    public async Task A_preview_changes_nothing()
    {
        var agent = await DeletionTests.SeedAsync(database);
        var run = await DeletionTests.FinishedRunAsync(database, agent, agent.VersionId, samples: 1);
        await ArchiveAsync(agent, agent.VersionId);

        (await server.Get($"/agents/{agent.Name}/versions/{agent.VersionId}/deletion")).EnsureSuccessStatusCode();

        Assert.True(await VersionExistsAsync(agent.VersionId));
        Assert.Equal((1, 1, 1), await RowsOfAsync(run));
    }

    [Fact]
    public async Task A_version_that_is_not_archived_is_refused_with_409_and_kept()
    {
        var agent = await DeletionTests.SeedAsync(database);
        var run = await DeletionTests.FinishedRunAsync(database, agent, agent.VersionId, samples: 1);

        var deleted = await server.Delete($"/agents/{agent.Name}/versions/{agent.VersionId}");
        var preview = await server.Get($"/agents/{agent.Name}/versions/{agent.VersionId}/deletion");

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
        Assert.Contains("archive it first", await deleted.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.True(await VersionExistsAsync(agent.VersionId));
        Assert.Equal((1, 1, 1), await RowsOfAsync(run));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_version_with_a_queued_or_running_run_is_refused_with_409_and_kept(bool running)
    {
        var agent = await DeletionTests.SeedAsync(database);
        var store = new RunStore(database.Pool);
        var run = await store.EnqueueAsync(agent.Id, agent.VersionId, [], agent.EndpointUrl, cancellationToken: Cancellation);
        if (running)
        {
            Assert.Equal(run, (await store.ClaimNextAsync(agent.Id, Cancellation))?.Id);
        }
        await ArchiveAsync(agent, agent.VersionId);

        var deleted = await server.Delete($"/agents/{agent.Name}/versions/{agent.VersionId}");

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Contains("queued or running", await deleted.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.True(await VersionExistsAsync(agent.VersionId));
        Assert.Equal(1, (await RowsOfAsync(run)).Runs);
    }

    [Fact]
    public async Task An_unknown_version_agent_or_a_version_of_another_agent_is_404()
    {
        var agent = await DeletionTests.SeedAsync(database);
        var stranger = await DeletionTests.SeedAsync(database);
        await ArchiveAsync(stranger, stranger.VersionId);

        var unknown = await server.Delete($"/agents/{agent.Name}/versions/{Guid.NewGuid()}");
        var foreign = await server.Delete($"/agents/{agent.Name}/versions/{stranger.VersionId}");
        var noAgent = await server.Delete($"/agents/no-such-agent/versions/{agent.VersionId}");
        var noPreview = await server.Get($"/agents/{agent.Name}/versions/{Guid.NewGuid()}/deletion");

        Assert.All([unknown, foreign, noAgent, noPreview], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.True(await VersionExistsAsync(stranger.VersionId));
    }
}
