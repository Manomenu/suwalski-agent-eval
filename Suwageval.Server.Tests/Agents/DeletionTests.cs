using System.Net;
using Dapper;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Api;
using Suwageval.Server.Tests.Runs;

namespace Suwageval.Server.Tests.Agents;

/// <summary>Deleting an archived version or a whole agent: the refusals, the preview and the cascade through runs, samples and verdicts — on a real PostgreSQL.</summary>
public sealed class DeletionTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private Task<SeededAgent> SeedAsync() =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

    private async Task<Guid> OtherVersionAsync(SeededAgent agent) =>
        (
            await new AgentStore(database.Pool).RegisterVersionAsync(
                agent.Id,
                SeededAgent.Serialized(SeededAgent.ExampleVersionConfigured("another config")),
                null,
                cancellationToken: Cancellation
            )
        )
            .Version
            .Id;

    /// <summary>A finished run of the version with <paramref name="samples"/> samples, each with one verdict; a re-check when <paramref name="recheckOf"/> is given.</summary>
    private async Task<Guid> FinishedRunAsync(SeededAgent agent, Guid versionId, int samples, Guid? recheckOf = null)
    {
        var store = new RunStore(database.Pool);
        var run = await store.EnqueueAsync(
            agent.Id,
            versionId,
            null,
            agent.EndpointUrl,
            recheckOf,
            recheckOf,
            cancellationToken: Cancellation
        );
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var check = Assert.Single(await new CheckStore(database.Pool).ListAsync(agent.Id, cancellationToken: Cancellation));
        for (var sample = 1; sample <= samples; sample++)
        {
            await store.AddSampleAsync(
                run,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    sample,
                    SampleStatus.Done,
                    /*lang=json,strict*/
                    """[{"kind":"AssistantMessage","text":"hi"}]""",
                    null,
                    10,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, true, "[]", "ok")]
                ),
                Cancellation
            );
        }
        await store.FinishAsync(run, RunStatus.Done, cancellationToken: Cancellation);
        return run;
    }

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
        var agent = await SeedAsync();
        var other = await OtherVersionAsync(agent);
        var first = await FinishedRunAsync(agent, agent.VersionId, samples: 2);
        var recheck = await FinishedRunAsync(agent, agent.VersionId, samples: 1, recheckOf: first);
        var kept = await FinishedRunAsync(agent, other, samples: 3);
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
        var agent = await SeedAsync();
        var run = await FinishedRunAsync(agent, agent.VersionId, samples: 1);
        await ArchiveAsync(agent, agent.VersionId);

        (await server.Get($"/agents/{agent.Name}/versions/{agent.VersionId}/deletion")).EnsureSuccessStatusCode();

        Assert.True(await VersionExistsAsync(agent.VersionId));
        Assert.Equal((1, 1, 1), await RowsOfAsync(run));
    }

    [Fact]
    public async Task A_version_that_is_not_archived_is_refused_with_409_and_kept()
    {
        var agent = await SeedAsync();
        var run = await FinishedRunAsync(agent, agent.VersionId, samples: 1);

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
        var agent = await SeedAsync();
        var store = new RunStore(database.Pool);
        var run = await store.EnqueueAsync(agent.Id, agent.VersionId, null, agent.EndpointUrl, cancellationToken: Cancellation);
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
        var agent = await SeedAsync();
        var stranger = await SeedAsync();
        await ArchiveAsync(stranger, stranger.VersionId);

        var unknown = await server.Delete($"/agents/{agent.Name}/versions/{Guid.NewGuid()}");
        var foreign = await server.Delete($"/agents/{agent.Name}/versions/{stranger.VersionId}");
        var noAgent = await server.Delete($"/agents/no-such-agent/versions/{agent.VersionId}");
        var noPreview = await server.Get($"/agents/{agent.Name}/versions/{Guid.NewGuid()}/deletion");

        Assert.All([unknown, foreign, noAgent, noPreview], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.True(await VersionExistsAsync(stranger.VersionId));
    }

    private async Task<(int Agents, int Versions, int Scenarios, int Checks, int Runs, int Samples, int Verdicts)> RowsOfAgentAsync(
        Guid agentId
    )
    {
        await using var connection = await database.Pool.OpenAsync(Cancellation);
        return await connection.QuerySingleAsync<(int, int, int, int, int, int, int)>(
            """
            SELECT (SELECT count(*) FROM agents WHERE id = @agentId)::int,
                   (SELECT count(*) FROM agent_versions WHERE agent_id = @agentId)::int,
                   (SELECT count(*) FROM scenarios WHERE agent_id = @agentId)::int,
                   (SELECT count(*) FROM checks WHERE agent_id = @agentId)::int,
                   (SELECT count(*) FROM runs WHERE agent_id = @agentId)::int,
                   (SELECT count(*) FROM scenario_results s JOIN runs r ON r.id = s.run_id WHERE r.agent_id = @agentId)::int,
                   (SELECT count(*) FROM check_results c JOIN scenario_results s ON s.id = c.scenario_result_id
                    JOIN runs r ON r.id = s.run_id WHERE r.agent_id = @agentId)::int
            """,
            new { agentId }
        );
    }

    [Fact]
    public async Task Deleting_an_agent_removes_everything_of_it_and_leaves_other_agents_alone()
    {
        var agent = await SeedAsync();
        var other = await OtherVersionAsync(agent);
        var first = await FinishedRunAsync(agent, agent.VersionId, samples: 2);
        await FinishedRunAsync(agent, agent.VersionId, samples: 1, recheckOf: first);
        await FinishedRunAsync(agent, other, samples: 1);
        var bystander = await SeedAsync();
        await FinishedRunAsync(bystander, bystander.VersionId, samples: 2);

        var preview = await server.Get($"/agents/{agent.Name}/deletion");
        var deleted = await server.Delete($"/agents/{agent.Name}");

        Assert.Equal(new AgentDeletion(agent.Id, agent.Name, 2, 1, 1, 3, 4, 4), await TestServer.Read<AgentDeletion>(preview));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), await RowsOfAgentAsync(agent.Id));
        Assert.Equal((1, 1, 1, 1, 1, 2, 2), await RowsOfAgentAsync(bystander.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get($"/agents/{agent.Name}")).StatusCode);
    }

    [Fact]
    public async Task An_agent_with_a_queued_run_is_refused_with_409_and_kept_and_a_preview_changes_nothing()
    {
        var agent = await SeedAsync();
        await new RunStore(database.Pool).EnqueueAsync(agent.Id, agent.VersionId, null, agent.EndpointUrl, cancellationToken: Cancellation);

        var preview = await server.Get($"/agents/{agent.Name}/deletion");
        var deleted = await server.Delete($"/agents/{agent.Name}");

        Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Contains("queued or running", await deleted.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Equal((1, 1, 1, 1, 1, 0, 0), await RowsOfAgentAsync(agent.Id));
    }

    [Fact]
    public async Task Deleting_an_unknown_agent_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await server.Delete("/agents/no-such-agent")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Get("/agents/no-such-agent/deletion")).StatusCode);
    }
}
