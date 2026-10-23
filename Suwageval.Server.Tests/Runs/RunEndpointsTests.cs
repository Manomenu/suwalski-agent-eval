using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
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

    private Task<SeededAgent> NewAgentAsync(string endpoint) =>
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
    public async Task A_run_keeps_the_label_its_contract_carried_though_the_version_keeps_its_first_one()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var first = await StartAsync(client, new RunRequest { Agent = agent.Name });
        fake.ServeContract(SeededAgent.ExampleVersion with { Label = "relabelled" });

        var second = await StartAsync(client, new RunRequest { Agent = agent.Name });

        Assert.Equal((first.AgentVersionId, false), (second.AgentVersionId, second.VersionCreated));
        var firstRun = await GetAsync<RunSummary>(client, $"/runs/{first.RunId}");
        var secondRun = await GetAsync<RunSummary>(client, $"/runs/{second.RunId}");
        Assert.Equal(SeededAgent.ExampleVersion.Label, firstRun.Label);
        Assert.Equal("relabelled", secondRun.Label);
    }

    [Fact]
    public async Task Starting_a_run_registers_the_contract_the_agent_serves_and_queues_the_run_with_it()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        fake.ServeContract(SeededAgent.ExampleVersionConfigured("A brand new prompt.") with { Label = "v2" });
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);

        var created = await StartAsync(client, new RunRequest { Agent = agent.Name, Tags = ["regression"] });

        Assert.True(created.VersionCreated);
        Assert.NotEqual(agent.VersionId, created.AgentVersionId);
        var run = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal((agent.Name, created.AgentVersionId, fake.Url.AbsoluteUri), (run.Agent, run.AgentVersionId, run.EndpointUrl));
        Assert.Equal(["regression"], run.Tags);
        Assert.Equal(RunStatus.Queued, run.Status);
        var version = await GetAsync<AgentVersionDetail>(client, $"/agents/{agent.Name}/versions/{created.AgentVersionId}");
        Assert.Equal(("A brand new prompt.", "v2"), (version.Content.Agents[0].Config, version.Content.Label));
    }

    [Fact]
    public async Task The_same_contract_is_the_same_version_and_a_changed_one_is_a_new_version()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);

        var first = await StartAsync(client, new RunRequest { Agent = agent.Name });
        var second = await StartAsync(client, new RunRequest { Agent = agent.Name });
        fake.ServeContract(SeededAgent.ExampleVersionConfigured("Changed."));
        var third = await StartAsync(client, new RunRequest { Agent = agent.Name });
        fake.ServeContract(SeededAgent.ExampleVersion);
        var back = await StartAsync(client, new RunRequest { Agent = agent.Name });

        // The seeded version has the example agent's content, so the first run finds it.
        Assert.Equal((agent.VersionId, false), (first.AgentVersionId, first.VersionCreated));
        Assert.Equal((first.AgentVersionId, false), (second.AgentVersionId, second.VersionCreated));
        Assert.NotEqual(first.AgentVersionId, third.AgentVersionId);
        Assert.True(third.VersionCreated);
        Assert.Equal((first.AgentVersionId, false), (back.AgentVersionId, back.VersionCreated));
        Assert.Equal(4, fake.ContractRequests);
    }

    [Fact]
    public async Task The_endpoint_of_the_request_is_where_the_contract_is_read_and_the_run_will_go()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync("http://127.0.0.1:1");

        var created = await StartAsync(client, new RunRequest { Agent = agent.Id.ToString(), EndpointUrl = fake.Url.AbsoluteUri });

        var run = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal(fake.Url.AbsoluteUri, run.EndpointUrl);
        Assert.Empty(run.Tags);
        Assert.Equal(1, fake.ContractRequests);
    }

    private static Task<HttpResponseMessage> Rerun(HttpClient client, Guid runId, RerunRequest? body = null) =>
        body is null
            ? client.PostAsync(new Uri($"/runs/{runId}/rerun", UriKind.Relative), null, Cancellation)
            : client.PostAsJsonAsync(new Uri($"/runs/{runId}/rerun", UriKind.Relative), body, Cancellation);

    private static async Task<RunCreated> RerunOkAsync(HttpClient client, Guid runId, RerunRequest? body = null)
    {
        var response = await Rerun(client, runId, body);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<RunCreated>(Cancellation) ?? throw new InvalidOperationException("no body");
    }

    [Fact]
    public async Task An_interrupted_run_can_be_run_again()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var original = await StartAsync(client, new RunRequest { Agent = agent.Name });
        var store = new RunStore(database.Pool);
        await store.ClaimNextAsync(agent.Id, Cancellation);
        await store.InterruptRunningAsync(agent.Id, Cancellation);

        var again = await RerunOkAsync(client, original.RunId);

        Assert.NotEqual(original.RunId, again.RunId);
        Assert.Equal(RunStatus.Interrupted, (await GetAsync<RunSummary>(client, $"/runs/{original.RunId}")).Status);
    }

    [Theory]
    [InlineData("regression")]
    [InlineData("regression", "smoke")]
    [InlineData]
    public async Task A_rerun_is_a_new_run_with_the_agent_tags_and_endpoint_of_the_original(params string[] tags)
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var original = await StartAsync(client, new RunRequest { Agent = agent.Name, Tags = tags });

        var again = await RerunOkAsync(client, original.RunId);

        Assert.NotEqual(original.RunId, again.RunId);
        Assert.Equal((original.AgentVersionId, false), (again.AgentVersionId, again.VersionCreated));
        var first = await GetAsync<RunSummary>(client, $"/runs/{original.RunId}");
        var second = await GetAsync<RunSummary>(client, $"/runs/{again.RunId}");
        Assert.Equal((agent.Name, fake.Url.AbsoluteUri), (second.Agent, second.EndpointUrl));
        Assert.Equal(tags, second.Tags);
        Assert.Equal((RunStatus.Queued, original.RunId), (first.Status, first.Id));
    }

    [Fact]
    public async Task A_rerun_takes_the_version_the_agent_serves_now_and_may_go_to_another_endpoint()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        await using var other = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var original = await StartAsync(client, new RunRequest { Agent = agent.Name });

        other.ServeContract(SeededAgent.ExampleVersionConfigured("Changed since."));
        var again = await RerunOkAsync(client, original.RunId, new RerunRequest { EndpointUrl = other.Url.AbsoluteUri });

        Assert.True(again.VersionCreated);
        Assert.NotEqual(original.AgentVersionId, again.AgentVersionId);
        Assert.Equal(other.Url.AbsoluteUri, (await GetAsync<RunSummary>(client, $"/runs/{again.RunId}")).EndpointUrl);
    }

    [Fact]
    public async Task Rerunning_an_unknown_run_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Rerun(Client(), Guid.NewGuid())).StatusCode);

    [Fact]
    public async Task A_rerun_meets_the_refusals_of_a_new_run()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var original = await StartAsync(client, new RunRequest { Agent = agent.Name });

        var response = await Rerun(client, original.RunId, new RerunRequest { EndpointUrl = "http://127.0.0.1:1" });

        await AssertBadGatewayAsync(response, "http://127.0.0.1:1/suwageval/contract");
        Assert.Single((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    [Fact]
    public async Task An_unknown_agent_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Post(Client(), new RunRequest { Agent = "no-such-agent" })).StatusCode);

    [Fact]
    public async Task A_version_in_the_request_is_not_accepted_any_more()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://127.0.0.1:1");

        var response = await client.PostAsJsonAsync(
            new Uri("/runs", UriKind.Relative),
            new { agent = agent.Name, agentVersionId = agent.VersionId },
            Cancellation
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", null, null, "agent")]
    [InlineData("x", "no-scenario-has-this", null, "tags")]
    [InlineData("x", null, "not a url", "endpointUrl")]
    [InlineData("x", null, "ftp://agent.test", "endpointUrl")]
    public async Task A_request_that_cannot_run_is_422_naming_the_field(string agentName, string? tag, string? endpoint, string field)
    {
        var client = Client();
        var agent = await NewAgentAsync("http://127.0.0.1:1");

        var response = await Post(
            client,
            new RunRequest
            {
                Agent = string.IsNullOrEmpty(agentName) ? "" : agent.Name,
                Tags = tag is null ? [] : [tag],
                EndpointUrl = endpoint,
            }
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task An_agent_that_cannot_be_reached_is_502_naming_the_url_and_nothing_is_queued()
    {
        var client = Client();
        var agent = await NewAgentAsync("http://127.0.0.1:1");

        var response = await Post(client, new RunRequest { Agent = agent.Name });

        await AssertBadGatewayAsync(response, "http://127.0.0.1:1/suwageval/contract");
        Assert.Empty((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    [Fact]
    public async Task A_contract_that_cannot_be_read_is_502_and_nothing_is_queued()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);

        fake.ServeContract(_ => Results.StatusCode(404));
        await AssertBadGatewayAsync(await Post(client, new RunRequest { Agent = agent.Name }), "404");
        fake.ServeContract(_ => Results.Text("<html>", "text/html"));
        await AssertBadGatewayAsync(await Post(client, new RunRequest { Agent = agent.Name }), "suwageval/contract");
        fake.ServeContract(_ =>
            Results.Text( /*lang=json,strict*/
                """{"agents":"p"}""",
                "application/json"
            )
        );
        await AssertBadGatewayAsync(await Post(client, new RunRequest { Agent = agent.Name }), "does not match the contract");
        fake.ServeContract(SeededAgent.ExampleVersion with { Agents = [] });
        await AssertBadGatewayAsync(await Post(client, new RunRequest { Agent = agent.Name }), "agents: there is at least one agent");

        Assert.Empty((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    private static async Task AssertBadGatewayAsync(HttpResponseMessage response, string detailPart)
    {
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Contains(detailPart, problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_version_that_does_not_fit_is_409_with_the_mismatches_and_nothing_is_queued()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        fake.ServeContract(
            new AgentContract
            {
                Agents =
                [
                    new LlmAgentDefinition
                    {
                        Name = "a",
                        Model = new ModelDefinition { Name = "m" },
                        Config = "p",
                    },
                ],
            }
        );
        var client = Client();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var response = await Post(client, new RunRequest { Agent = agent.Name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        var mismatches = problem.RootElement.GetProperty("mismatches").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Equal(2, mismatches.Count);
        Assert.Contains(mismatches, m => m!.Contains("refund_order", StringComparison.Ordinal));
        Assert.Contains(mismatches, m => m!.Contains("where.after", StringComparison.Ordinal));
        Assert.Empty((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    [Fact]
    public async Task A_judge_check_without_a_configured_judge_model_is_409_and_nothing_is_queued()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        await using var withoutJudge = app.WithWebHostBuilder(host => host.UseSetting("Judge:Model", ""));
        var client = withoutJudge.CreateClient();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [
                new JudgeCheck
                {
                    Key = "polite",
                    ScenarioKey = "refund",
                    Criteria = "The assistant is polite.",
                },
            ]
        );

        var response = await Post(client, new RunRequest { Agent = agent.Name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("No judge model is configured", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Empty((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    [Fact]
    public async Task A_judge_matcher_without_a_configured_judge_model_is_409_and_nothing_is_queued()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        await using var withoutJudge = app.WithWebHostBuilder(host => host.UseSetting("Judge:Model", ""));
        var client = withoutJudge.CreateClient();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [
                new ToolCalledCheck
                {
                    Key = "reason-fits",
                    ScenarioKey = "refund",
                    Tool = "refund_order",
                    Args = JsonDocument
                        .Parse( /*lang=json,strict*/
                            """{"reason":{"$judge":{"description":"damaged"}}}"""
                        )
                        .RootElement.Clone(),
                },
            ]
        );

        var response = await Post(client, new RunRequest { Agent = agent.Name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("No judge model is configured", body, StringComparison.Ordinal);
        Assert.Contains("argument matching", body, StringComparison.Ordinal);
        Assert.Empty((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    [Fact]
    public async Task Runs_of_an_agent_are_listed_newest_first()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var first = await StartAsync(client, new RunRequest { Agent = agent.Name });
        var second = await StartAsync(client, new RunRequest { Agent = agent.Name });
        await NewAgentAsync(fake.Url.AbsoluteUri);

        var runs = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}");

        Assert.Equal([second.RunId, first.RunId], runs.Runs.Select(run => run.Id));
        Assert.Equal(runs.Runs.Count, runs.TotalCount);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(new Uri("/runs?agent=nope", UriKind.Relative), Cancellation)).StatusCode
        );
    }

    [Fact]
    public async Task A_run_started_in_a_group_is_listed_with_it_and_the_list_and_the_groups_follow_it()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var first = await StartAsync(client, new RunRequest { Agent = agent.Name, Group = "sweep-1" });
        var second = await StartAsync(client, new RunRequest { Agent = agent.Name, Group = "sweep-1" });
        await StartAsync(client, new RunRequest { Agent = agent.Name });

        var inGroup = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&group=sweep-1");
        var groups = await GetAsync<List<RunGroupSummary>>(client, $"/agents/{agent.Name}/run-groups");

        Assert.Equal([second.RunId, first.RunId], inGroup.Runs.Select(run => run.Id));
        Assert.All(inGroup.Runs, run => Assert.Equal("sweep-1", run.Group));
        Assert.Equal(("sweep-1", 2, 1), (groups.Single().Name, groups.Single().Runs, groups.Single().Versions));
    }

    [Fact]
    public async Task A_rerun_is_in_the_group_it_asks_for_and_does_not_inherit_the_original_s()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var original = await StartAsync(client, new RunRequest { Agent = agent.Name, Group = "sweep-1" });

        var plain = await RerunOkAsync(client, original.RunId);
        var grouped = await RerunOkAsync(client, original.RunId, new RerunRequest { Group = "sweep-2" });

        Assert.Null((await GetAsync<RunSummary>(client, $"/runs/{plain.RunId}")).Group);
        Assert.Equal("sweep-2", (await GetAsync<RunSummary>(client, $"/runs/{grouped.RunId}")).Group);
    }

    [Theory]
    [InlineData("Sweep 1")]
    [InlineData("sweep--1")]
    public async Task A_group_that_is_not_kebab_case_is_422_naming_the_field_and_nothing_is_queued(string group)
    {
        var client = Client();
        var agent = await NewAgentAsync("http://127.0.0.1:1");

        var response = await Post(client, new RunRequest { Agent = agent.Name, Group = group });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("group", out _));
        Assert.Empty((await GetAsync<RunList>(client, $"/runs?agent={agent.Name}")).Runs);
    }

    [Fact]
    public async Task The_run_list_is_paged_by_page_and_page_size_with_the_total_and_a_page_past_the_end_is_the_last()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var started = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            started.Add((await StartAsync(client, new RunRequest { Agent = agent.Name })).RunId);
        }
        started.Reverse();

        var first = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&pageSize=2");
        var second = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&pageSize=2&page=2");
        var pastTheEnd = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&pageSize=2&page=9");

        Assert.Equal(started.Take(2), first.Runs.Select(run => run.Id));
        Assert.Equal(started.Skip(2), second.Runs.Select(run => run.Id));
        Assert.Equal((1, 2, 3), (first.Page, second.Page, first.TotalCount));
        Assert.Equal(second.Runs.Select(run => run.Id), pastTheEnd.Runs.Select(run => run.Id));
        Assert.Equal((2, 3), (pastTheEnd.Page, pastTheEnd.TotalCount));
    }

    [Fact]
    public async Task Repeated_tag_parameters_keep_the_runs_of_any_of_the_tags_and_the_unlimited_ones()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var store = new RunStore(database.Pool);
        var smoke = await store.EnqueueAsync(agent.Id, agent.VersionId, ["smoke"], agent.EndpointUrl, cancellationToken: Cancellation);
        await store.EnqueueAsync(agent.Id, agent.VersionId, ["billing"], agent.EndpointUrl, cancellationToken: Cancellation);
        var everything = await store.EnqueueAsync(agent.Id, agent.VersionId, [], agent.EndpointUrl, cancellationToken: Cancellation);

        var one = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&tag=smoke");
        var two = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&tag=smoke&tag=billing");

        Assert.Equal(new[] { everything, smoke }.Order(), one.Runs.Select(run => run.Id).Order());
        Assert.Equal((2, 3), (one.TotalCount, two.TotalCount));
    }

    [Fact]
    public async Task The_total_counts_only_the_runs_a_filter_leaves()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        await StartAsync(client, new RunRequest { Agent = agent.Name });
        await StartAsync(client, new RunRequest { Agent = agent.Name, Group = "sweep-1" });
        await StartAsync(client, new RunRequest { Agent = agent.Name, Group = "sweep-1" });

        var inGroup = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&group=sweep-1&pageSize=1");
        var outsideGroups = await GetAsync<RunList>(client, $"/runs?agent={agent.Name}&grouping=None&pageSize=1");

        Assert.Equal((1, 2), (inGroup.Runs.Count, inGroup.TotalCount));
        Assert.Equal((1, 1), (outsideGroups.Runs.Count, outsideGroups.TotalCount));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task A_bad_page_or_page_size_is_422(string query)
    {
        var response = await Client().GetAsync(new Uri($"/runs?{query}", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
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
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var created = await StartAsync(client, new RunRequest { Agent = agent.Name });

        var response = await client.PostAsync(new Uri($"/runs/{created.RunId}/cancel", UriKind.Relative), null, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var run = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.True(run.CancelRequested);
    }

    [Fact]
    public async Task A_queued_run_has_empty_results()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var created = await StartAsync(client, new RunRequest { Agent = agent.Name });

        var results = await GetAsync<RunResults>(client, $"/runs/{created.RunId}/results");

        Assert.Equal(RunStatus.Queued, results.Run.Status);
        Assert.Empty(results.Scenarios);
        Assert.Null(results.MedianMs);
        Assert.Null(results.P95Ms);
    }

    [Fact]
    public async Task A_finished_run_serves_its_results_with_pass_rates_and_evidence()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var created = await StartAsync(client, new RunRequest { Agent = agent.Name, Tags = ["regression"] });

        var run = await app.Services.GetRequiredService<RunStore>().ClaimNextAsync(agent.Id, Cancellation);
        await app
            .Services.GetRequiredService<RunExecutor>()
            .ExecuteAsync(run ?? throw new InvalidOperationException("not queued"), Cancellation);

        var summary = await GetAsync<RunSummary>(client, $"/runs/{created.RunId}");
        Assert.Equal(
            (RunStatus.Done, 1, 1, 2, 0, false),
            (
                summary.Status,
                summary.SamplesTotal,
                summary.SamplesDone,
                summary.ChecksPassed,
                summary.ChecksFailed,
                summary.ContractChangedDuringRun
            )
        );
        var results = await GetAsync<RunResults>(client, $"/runs/{created.RunId}/results");
        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal("refund", scenario.Key);
        Assert.Equal(6, scenario.Samples[0].Output?.Count);
        Assert.Empty(scenario.Samples[0].Undeclared);
        Assert.All(scenario.Checks, check => Assert.True(check.Passed && check.PassRate == 1.0 && check.PassThreshold == 1.0));
        Assert.All(scenario.Checks, check => Assert.Equal((check.Revision, check.Key), (check.LatestRevision, check.Definition.Key)));
        Assert.Equal(
            "refund_order",
            Assert.IsType<ToolCalledCheck>(scenario.Checks.Single(check => check.Key == "refund-called").Definition).Tool
        );
        Assert.IsType<Event>(Assert.Single(scenario.Checks.Single(check => check.Key == "refund-event").Samples[0].Evidence));
        Assert.All(scenario.Checks, check => Assert.False(check.JudgedByModel));
        var duration = Assert.NotNull(scenario.Samples[0].DurationMs);
        Assert.Equal(duration, results.MedianMs);
        Assert.Equal(duration, results.P95Ms);
    }

    [Fact]
    public async Task Pass_rates_list_the_finished_runs_oldest_first_with_the_rate_per_tag()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var client = Client();
        var agent = await NewAgentAsync(fake.Url.AbsoluteUri);
        var queued = await StartAsync(client, new RunRequest { Agent = agent.Name, Tags = ["regression"] });
        var done = await StartAsync(client, new RunRequest { Agent = agent.Name, Tags = ["regression"] });
        var store = app.Services.GetRequiredService<RunStore>();
        var claimed = await store.ClaimNextAsync(agent.Id, Cancellation);
        await app
            .Services.GetRequiredService<RunExecutor>()
            .ExecuteAsync(claimed ?? throw new InvalidOperationException("not queued"), Cancellation);

        var points = await GetAsync<List<RunPassRates>>(client, $"/agents/{agent.Name}/pass-rates");

        var point = Assert.Single(points);
        Assert.Equal((queued.RunId, agent.VersionId, 1.0), (point.RunId, point.AgentVersionId, point.PassRate));
        var tag = Assert.Single(point.Tags);
        Assert.Equal(("regression", 1.0), (tag.Tag, tag.PassRate));
        Assert.True(point.MedianMs >= 0 && point.P95Ms >= point.MedianMs, $"{point.MedianMs} {point.P95Ms}");
        Assert.Equal((point.MedianMs, point.P95Ms), (tag.MedianMs, tag.P95Ms));
        Assert.Equal(["regression"], point.LimitedToTags);
        Assert.NotEqual(done.RunId, point.RunId);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(new Uri("/agents/no-such-agent/pass-rates", UriKind.Relative), Cancellation)).StatusCode
        );
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
