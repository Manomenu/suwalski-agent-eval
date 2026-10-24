using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents.Versions;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Runs.Unstable;

public sealed class UnstableEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    private Task<SeededAgent> NewAgentAsync(string endpoint) =>
        SeededAgent.CreateAsync(
            database.Pool,
            endpoint,
            [SeededAgent.Scenario("refund", ["regression"]), SeededAgent.Scenario("other", ["smoke"])],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );

    private static async Task<T> GetAsync<T>(HttpClient client, string url) =>
        await client.GetFromJsonAsync<T>(new Uri(url, UriKind.Relative), ContractJson.Options, Cancellation)
        ?? throw new InvalidOperationException("no body");

    [Fact]
    public async Task Unstable_checks_list_a_check_that_passed_in_one_finished_run_and_failed_in_another()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://agent.test");
        var store = app.Services.GetRequiredService<RunStore>();
        var scenario = await app.Services.GetRequiredService<ScenarioStore>().GetAsync(agent.Id, "refund", Cancellation);
        var check = (await app.Services.GetRequiredService<CheckStore>().ListAsync(agent.Id, cancellationToken: Cancellation)).First(
            check => check.Key == "refund-called"
        );
        var failedRun = Guid.Empty;
        foreach (var passed in new[] { true, false })
        {
            var id = await store.EnqueueAsync(agent.Id, agent.VersionId, [], agent.EndpointUrl, cancellationToken: Cancellation);
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    1,
                    SampleStatus.Done,
                    "[]",
                    null,
                    1,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, passed, "[]", "m")]
                ),
                Cancellation
            );
            await store.FinishAsync(id, RunStatus.Done, cancellationToken: Cancellation);
            failedRun = passed ? failedRun : id;
        }

        var unstable = Assert.Single(await GetAsync<List<UnstableCheck>>(client, $"/agents/{agent.Name}/unstable-checks"));

        Assert.Equal(
            ("refund", "refund-called", agent.VersionId, 2, 1, failedRun),
            (
                unstable.ScenarioKey,
                unstable.CheckKey,
                unstable.AgentVersionId,
                unstable.Runs,
                unstable.PassedRuns,
                unstable.NewestFailedRunId
            )
        );
        Assert.Empty(await GetAsync<List<UnstableCheck>>(client, $"/agents/{agent.Name}/unstable-checks?version={Guid.NewGuid()}"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(new Uri("/agents/no-such-agent/unstable-checks", UriKind.Relative), Cancellation)).StatusCode
        );
    }

    [Fact]
    public async Task An_unstable_check_closes_when_a_newer_version_runs_it_twice_without_the_problem()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://agent.test");
        await RunHistory.AddRunAsync(app.Services, agent, agent.VersionId, true);
        await RunHistory.AddRunAsync(app.Services, agent, agent.VersionId, false);
        var newer = await RunHistory.NewerVersionAsync(app.Services, agent, "newer");
        var url = $"/agents/{agent.Name}/unstable-checks";

        await RunHistory.AddRunAsync(app.Services, agent, newer, true);
        Assert.Equal(agent.VersionId, Assert.Single(await GetAsync<List<UnstableCheck>>(client, url)).AgentVersionId);

        await RunHistory.AddRunAsync(app.Services, agent, newer, true);
        Assert.Empty(await GetAsync<List<UnstableCheck>>(client, url));
        Assert.Equal(
            agent.VersionId,
            Assert.Single(await GetAsync<List<UnstableCheck>>(client, $"{url}?version={agent.VersionId}")).AgentVersionId
        );
    }

    [Fact]
    public async Task An_archived_version_is_left_out_of_the_open_unstable_checks_unless_asked_for()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://agent.test");
        await RunHistory.AddRunAsync(app.Services, agent, agent.VersionId, true);
        await RunHistory.AddRunAsync(app.Services, agent, agent.VersionId, false);
        await app.Services.GetRequiredService<VersionStore>().ArchiveVersionsAsync(agent.Id, [agent.VersionId], true, Cancellation);
        var url = $"/agents/{agent.Name}/unstable-checks";

        Assert.Empty(await GetAsync<List<UnstableCheck>>(client, url));
        Assert.Single(await GetAsync<List<UnstableCheck>>(client, $"{url}?version={agent.VersionId}"));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
