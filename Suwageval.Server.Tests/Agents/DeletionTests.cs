using System.Net;
using Dapper;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Tests.Api;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests.Agents;

/// <summary>Deleting a whole agent: the refusals, the preview and the cascade through runs, samples and verdicts — on a real PostgreSQL.</summary>
public sealed class DeletionTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => server.DisposeAsync();

    internal static Task<SeededAgent> SeedAsync(TestDatabase database) =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

    internal static async Task<Guid> OtherVersionAsync(TestDatabase database, SeededAgent agent) =>
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
    internal static async Task<Guid> FinishedRunAsync(
        TestDatabase database,
        SeededAgent agent,
        Guid versionId,
        int samples,
        Guid? recheckOf = null
    )
    {
        var store = new RunStore(database.Pool);
        var run = await store.EnqueueAsync(
            agent.Id,
            versionId,
            [],
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
        var agent = await SeedAsync(database);
        var other = await OtherVersionAsync(database, agent);
        var first = await FinishedRunAsync(database, agent, agent.VersionId, samples: 2);
        await FinishedRunAsync(database, agent, agent.VersionId, samples: 1, recheckOf: first);
        await FinishedRunAsync(database, agent, other, samples: 1);
        var bystander = await SeedAsync(database);
        await FinishedRunAsync(database, bystander, bystander.VersionId, samples: 2);

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
        var agent = await SeedAsync(database);
        await new RunStore(database.Pool).EnqueueAsync(agent.Id, agent.VersionId, [], agent.EndpointUrl, cancellationToken: Cancellation);

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
