using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class RunEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    private Task<SeededAgent> NewAgentAsync(string endpoint = "http://agent.test") =>
        SeededAgent.CreateAsync(
            database.Pool,
            endpoint,
            [SeededAgent.Scenario("refund", ["regression"]), SeededAgent.Scenario("other", ["smoke"])],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );

    private static Task<HttpResponseMessage> Post(HttpClient client, RunRequest request) =>
        client.PostAsJsonAsync(new Uri("/runs", UriKind.Relative), request, Cancellation);

    private static async Task<RunCreated> StartAsync(HttpClient client, RunRequest request)
    {
        var response = await Post(client, request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<RunCreated>(Cancellation) ?? throw new InvalidOperationException("no body");
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url) =>
        await client.GetFromJsonAsync<T>(new Uri(url, UriKind.Relative), ContractJson.Options, Cancellation)
        ?? throw new InvalidOperationException("no body");

    [Fact]
    public async Task Starting_a_run_queues_it_and_the_newest_version_is_the_default()
    {
        var client = Client();
        var agent = await NewAgentAsync();
        var newer = await new AgentStore(database.Pool).RegisterVersionAsync(
            agent.Id,
            JsonSerializer.Serialize(SeededAgent.ExampleVersion with { Label = "newer" }, ContractJson.Options),
            "newer",
            Cancellation
        );

        var created = await StartAsync(client, new RunRequest { Agent = agent.Name, Tag = "regression" });

        var run = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal(
            (agent.Name, newer.Version.Id, "regression", "http://agent.test"),
            (run.Agent, run.AgentVersionId, run.Tag, run.EndpointUrl)
        );
        Assert.Equal(RunStatus.Queued, run.Status);
    }

    [Fact]
    public async Task A_run_can_name_the_agent_by_id_pick_a_version_and_override_the_endpoint()
    {
        var client = Client();
        var agent = await NewAgentAsync();

        var created = await StartAsync(
            client,
            new RunRequest
            {
                Agent = agent.Id.ToString(),
                AgentVersionId = agent.VersionId,
                EndpointUrl = "http://other.test:7010",
            }
        );

        var run = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal((agent.VersionId, "http://other.test:7010", null), (run.AgentVersionId, run.EndpointUrl, run.Tag));
    }

    [Fact]
    public async Task An_unknown_agent_or_version_is_404()
    {
        var client = Client();
        var agent = await NewAgentAsync();
        var other = await NewAgentAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, new RunRequest { Agent = "no-such-agent" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Post(client, new RunRequest { Agent = agent.Name, AgentVersionId = Guid.NewGuid() })).StatusCode
        );
        // A version of another agent is not this agent's version.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Post(client, new RunRequest { Agent = agent.Name, AgentVersionId = other.VersionId })).StatusCode
        );
    }

    [Fact]
    public async Task An_agent_without_a_version_cannot_be_run()
    {
        var client = Client();
        var name = Agents.AgentStoreTests.UniqueName();
        await new AgentStore(database.Pool).CreateAsync(name, "http://agent.test", Cancellation);

        var response = await Post(client, new RunRequest { Agent = name });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("push it first", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", null, null, "agent")]
    [InlineData("x", "no-scenario-has-this", null, "tag")]
    [InlineData("x", null, "not a url", "endpointUrl")]
    [InlineData("x", null, "ftp://agent.test", "endpointUrl")]
    public async Task A_request_that_cannot_run_is_422_naming_the_field(string agentName, string? tag, string? endpoint, string field)
    {
        var client = Client();
        var agent = await NewAgentAsync();

        var response = await Post(
            client,
            new RunRequest
            {
                Agent = string.IsNullOrEmpty(agentName) ? "" : agent.Name,
                Tag = tag,
                EndpointUrl = endpoint,
            }
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task A_version_that_does_not_fit_is_409_with_the_mismatches_and_nothing_is_queued()
    {
        var client = Client();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")],
            new AgentVersionInput
            {
                SystemPrompt = "p",
                Model = new ModelDefinition { Name = "m" },
            }
        );

        var response = await Post(client, new RunRequest { Agent = agent.Name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        var mismatches = problem.RootElement.GetProperty("mismatches").EnumerateArray().Select(m => m.GetString()).ToList();
        var one = Assert.Single(mismatches);
        Assert.Contains("refund_order", one, StringComparison.Ordinal);
        Assert.Empty(await GetAsync<List<RunSummary>>(client, $"/runs?agent={agent.Name}"));
    }

    [Fact]
    public async Task Runs_of_an_agent_are_listed_newest_first()
    {
        var client = Client();
        var agent = await NewAgentAsync();
        var first = await StartAsync(client, new RunRequest { Agent = agent.Name });
        var second = await StartAsync(client, new RunRequest { Agent = agent.Name });
        await NewAgentAsync();

        var runs = await GetAsync<List<RunSummary>>(client, $"/runs?agent={agent.Name}");

        Assert.Equal([second.RunId, first.RunId], runs.Select(run => run.Id));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(new Uri("/runs?agent=nope", UriKind.Relative), Cancellation)).StatusCode
        );
    }

    [Fact]
    public async Task An_unknown_run_is_404_everywhere()
    {
        var client = Client();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri($"/runs/{id}", UriKind.Relative), Cancellation)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(new Uri($"/runs/{id}/results", UriKind.Relative), Cancellation)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync(new Uri($"/runs/{id}/cancel", UriKind.Relative), null, Cancellation)).StatusCode
        );
    }

    [Fact]
    public async Task Cancelling_a_queued_run_is_202_and_the_run_is_cancelled()
    {
        var client = Client();
        var agent = await NewAgentAsync();
        var created = await StartAsync(client, new RunRequest { Agent = agent.Name });

        var response = await client.PostAsync(new Uri($"/runs/{created.RunId}/cancel", UriKind.Relative), null, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(RunStatus.Cancelled, (await GetAsync<RunSummary>(client, $"/runs/{created.RunId}")).Status);
    }

    [Fact]
    public async Task A_queued_run_has_empty_results()
    {
        var client = Client();
        var agent = await NewAgentAsync();
        var created = await StartAsync(client, new RunRequest { Agent = agent.Name });

        var results = await GetAsync<RunResults>(client, $"/runs/{created.RunId}/results");

        Assert.Equal(RunStatus.Queued, results.Run.Status);
        Assert.Empty(results.Scenarios);
    }

    [Fact]
    public async Task A_finished_run_serves_its_results_with_pass_rates_and_evidence()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var created = await StartAsync(client, new RunRequest { Agent = agent.Name, Tag = "regression" });

        var run = await app.Services.GetRequiredService<RunStore>().ClaimNextAsync(agent.Id, Cancellation);
        await app
            .Services.GetRequiredService<RunExecutor>()
            .ExecuteAsync(run ?? throw new InvalidOperationException("not queued"), Cancellation);

        var summary = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal(
            (RunStatus.Done, 1, 1, 2, 0),
            (summary.Status, summary.SamplesTotal, summary.SamplesDone, summary.ChecksPassed, summary.ChecksFailed)
        );
        var results = await GetAsync<RunResults>(client, $"/runs/{created.RunId}/results");
        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal("refund", scenario.Key);
        Assert.Equal(6, scenario.Samples[0].Output?.Count);
        Assert.All(scenario.Checks, check => Assert.True(check.Passed && check.PassRate == 1.0 && check.PassThreshold == 1.0));
        Assert.IsType<Event>(Assert.Single(scenario.Checks.Single(check => check.Key == "refund-event").Samples[0].Evidence));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
