using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Runs.Variants;

/// <summary>The variants view over runs stored in the real database: its columns, tags and scenario cells, and what it refuses.</summary>
public sealed class VariantEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Task<SeededAgent> AgentAsync(TestDatabase database) =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund", ["billing"]), SeededAgent.Scenario("plain", [])],
            [
                SeededAgent.RefundAfterLookup("refund-called", "refund"),
                SeededAgent.RefundedEvent("refund-event", "refund"),
                SeededAgent.RefundAfterLookup("plain-called", "plain"),
            ]
        );

    /// <summary>A finished run with one sample of "refund" (the verdicts given) and, unless <paramref name="onlyRefund"/>, one of "plain" that passes.</summary>
    private async Task<Guid> RunAsync(SeededAgent agent, string? group, bool refundCalled, bool refundEvent, bool onlyRefund = false)
    {
        var store = app.Services.GetRequiredService<RunStore>();
        var scenarios = app.Services.GetRequiredService<ScenarioStore>();
        var checks = await app.Services.GetRequiredService<CheckStore>().ListAsync(agent.Id, cancellationToken: Cancellation);
        var id = await store.EnqueueAsync(
            agent.Id,
            agent.VersionId,
            [],
            agent.EndpointUrl,
            runGroup: group,
            cancellationToken: Cancellation
        );
        async Task AddAsync(string scenarioKey, params (string Check, bool Passed)[] verdicts)
        {
            var scenario = await scenarios.GetAsync(agent.Id, scenarioKey, Cancellation);
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    1,
                    SampleStatus.Done,
                    "[]",
                    null,
                    1000,
                    [],
                    [
                        .. verdicts.Select(verdict => new NewCheckResult(
                            checks.First(check => check.Key == verdict.Check).Id,
                            1,
                            verdict.Passed,
                            "[]",
                            "m"
                        )),
                    ]
                ),
                Cancellation
            );
        }
        await AddAsync("refund", ("refund-called", refundCalled), ("refund-event", refundEvent));
        if (!onlyRefund)
        {
            await AddAsync("plain", ("plain-called", true));
        }
        await store.FinishAsync(id, RunStatus.Done, cancellationToken: Cancellation);
        return id;
    }

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        _ = database.Pool;
        return await app.CreateClient().GetAsync(new Uri(url, UriKind.Relative), Cancellation);
    }

    [Fact]
    public async Task Runs_named_one_by_one_are_columns_with_a_cell_per_scenario_and_a_rate_per_tag()
    {
        var agent = await AgentAsync(database);
        var weak = await RunAsync(agent, null, refundCalled: true, refundEvent: false);
        var strong = await RunAsync(agent, null, refundCalled: true, refundEvent: true, onlyRefund: true);

        var variants = await TestServer.Read<RunVariants>(await GetAsync($"/runs/variants?runs={weak}&runs={strong}"));

        Assert.Equal((agent.Name, null), (variants.Agent, variants.Group));
        Assert.Equal([weak, strong], variants.Runs.Select(run => run.Run.Id));
        Assert.Equal([1000.0, 1000.0], variants.Runs.Select(run => run.MedianMs));
        Assert.Equal(["plain", "refund"], variants.Scenarios.Select(scenario => scenario.Key));
        var refund = variants.Scenarios.Single(scenario => scenario.Key == "refund");
        Assert.Equal(["billing"], refund.Tags);
        Assert.Equal((1, 2), (refund.Cells[0]!.ChecksPassed, refund.Cells[0]!.ChecksTotal));
        Assert.Equal(["refund-event"], refund.Cells[0]!.FailedChecks);
        var failing = refund.Cells[0]!.Checks.Single(check => check.Key == "refund-event");
        Assert.Equal((false, 0, 1), (failing.Passed, failing.SamplesPassed, failing.Samples));
        Assert.Equal([(1, "m")], failing.Failures.Select(failure => (failure.Sample, failure.Message)));
        Assert.Empty(refund.Cells[1]!.Checks.Single(check => check.Key == "refund-event").Failures);
        Assert.Equal((2, 2), (refund.Cells[1]!.ChecksPassed, refund.Cells[1]!.ChecksTotal));
        Assert.Null(variants.Scenarios.Single(scenario => scenario.Key == "plain").Cells[1]);
        Assert.Equal(
            ["billing|0.5|1"],
            variants.Tags.Where(row => row.Tag == "billing").Select(row => $"{row.Tag}|{row.Rates[0].PassRate}|{row.Rates[1].PassRate}")
        );
    }

    [Fact]
    public async Task A_group_is_its_runs_oldest_first_and_only_the_agents_that_ask_for_it()
    {
        var agent = await AgentAsync(database);
        var other = await AgentAsync(database);
        var first = await RunAsync(agent, "sweep-1", true, true);
        var second = await RunAsync(agent, "sweep-1", true, false);
        await RunAsync(agent, "sweep-2", true, true);

        var variants = await TestServer.Read<RunVariants>(await GetAsync($"/runs/variants?group=sweep-1&agent={agent.Name}"));
        await RunAsync(other, "sweep-1", true, true);
        var several = await GetAsync("/runs/variants?group=sweep-1");

        Assert.Equal("sweep-1", variants.Group);
        Assert.Equal([first, second], variants.Runs.Select(run => run.Run.Id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, several.StatusCode);
        Assert.Equal(["group"], await TestServer.ErrorFields(several));
    }

    [Fact]
    public async Task Variants_refuse_runs_of_different_agents_both_or_neither_selection_and_know_no_unknown_run_or_group()
    {
        var agent = await AgentAsync(database);
        var other = await AgentAsync(database);
        var one = await RunAsync(agent, null, true, true);
        var foreign = await RunAsync(other, null, true, true);

        var differentAgents = await GetAsync($"/runs/variants?runs={one}&runs={foreign}");
        var both = await GetAsync($"/runs/variants?runs={one}&group=sweep-1");
        var neither = await GetAsync("/runs/variants");
        var unknownRun = await GetAsync($"/runs/variants?runs={Guid.NewGuid()}");
        var unknownGroup = await GetAsync($"/runs/variants?group=no-such-group&agent={agent.Name}");

        Assert.Equal(["runs"], await TestServer.ErrorFields(differentAgents));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, both.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, neither.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownRun.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownGroup.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
