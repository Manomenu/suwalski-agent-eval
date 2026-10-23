using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Tests.Runs.AgentErrors;

public sealed class AgentErrorEndpointsTests(TestDatabase database) : IAsyncDisposable
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
    public async Task Agent_errors_list_the_scenarios_where_the_agent_gave_no_answer_and_ignore_rechecks()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://agent.test");
        var store = app.Services.GetRequiredService<RunStore>();
        var scenario = (await app.Services.GetRequiredService<ScenarioStore>().GetAsync(agent.Id, "refund", Cancellation))!;
        async Task<Guid> FinishedRunAsync(SampleStatus status, string? error, Guid? recheckOf = null)
        {
            var id = await store.EnqueueAsync(
                agent.Id,
                agent.VersionId,
                [],
                agent.EndpointUrl,
                recheckOf: recheckOf,
                cancellationToken: Cancellation
            );
            await store.AddSampleAsync(
                id,
                new NewSample(scenario.Id, scenario.Revision, 1, status, status == SampleStatus.Done ? "[]" : null, error, null, [], []),
                Cancellation
            );
            await store.FinishAsync(id, RunStatus.Done, cancellationToken: Cancellation);
            return id;
        }
        await FinishedRunAsync(SampleStatus.Done, null);
        var failed = await FinishedRunAsync(SampleStatus.Failed, "HTTP 500");
        await FinishedRunAsync(SampleStatus.Failed, "from a recheck", recheckOf: failed);

        var error = Assert.Single(await GetAsync<List<AgentError>>(client, $"/agents/{agent.Name}/agent-errors"));

        Assert.Equal(
            ("refund", agent.VersionId, 1, 2, failed, "HTTP 500"),
            (error.ScenarioKey, error.AgentVersionId, error.Errors, error.Samples, error.NewestErrorRunId, error.NewestError)
        );
        Assert.Empty(await GetAsync<List<AgentError>>(client, $"/agents/{agent.Name}/agent-errors?version={Guid.NewGuid()}"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(new Uri("/agents/no-such-agent/agent-errors", UriKind.Relative), Cancellation)).StatusCode
        );
    }

    [Fact]
    public async Task An_agent_error_closes_when_a_newer_version_runs_the_scenario_without_it()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://agent.test");
        await RunHistory.AddRunAsync(app.Services, agent, agent.VersionId, null);
        var url = $"/agents/{agent.Name}/agent-errors";
        Assert.Single(await GetAsync<List<AgentError>>(client, url));
        var newer = await RunHistory.NewerVersionAsync(app.Services, agent, "newer");

        await RunHistory.AddRunAsync(app.Services, agent, newer, true);

        Assert.Empty(await GetAsync<List<AgentError>>(client, url));
        Assert.Single(await GetAsync<List<AgentError>>(client, $"{url}?version={agent.VersionId}"));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
