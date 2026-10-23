using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Runs.Compare;

/// <summary>
/// The compare view over runs stored in the real database: its columns, tags and scenario cells, the baseline that adds the changes, and
/// what it refuses.
/// </summary>
public sealed class CompareEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>One stored sample: the scenario, its number, the duration and the verdicts (check key, passed, judge model).</summary>
    private sealed record Sample(
        string Scenario,
        int Number,
        int? DurationMs,
        params (string Check, bool Passed, string? Judge)[] Verdicts
    );

    private async Task<Guid> RunAsync(SeededAgent agent, string? group, params Sample[] samples)
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
        foreach (var sample in samples)
        {
            var scenario = await scenarios.GetAsync(agent.Id, sample.Scenario, Cancellation);
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    sample.Number,
                    SampleStatus.Done,
                    "[]",
                    null,
                    sample.DurationMs,
                    [],
                    [
                        .. sample.Verdicts.Select(verdict => new NewCheckResult(
                            checks.First(check => check.Key == verdict.Check).Id,
                            1,
                            verdict.Passed,
                            "[]",
                            "m",
                            JudgeModel: verdict.Judge
                        )),
                    ]
                ),
                Cancellation
            );
        }
        await store.FinishAsync(id, RunStatus.Done, cancellationToken: Cancellation);
        return id;
    }

    private Task<Guid> RunAsync(SeededAgent agent, params Sample[] samples) => RunAsync(agent, null, samples);

    /// <summary>A run with one sample of "refund" (the verdicts given) and, unless <paramref name="onlyRefund"/>, one of "plain" that passes.</summary>
    private Task<Guid> VariantAsync(SeededAgent agent, string? group, bool refundCalled, bool refundEvent, bool onlyRefund = false) =>
        RunAsync(
            agent,
            group,
            onlyRefund
                ? [new Sample("refund", 1, 1000, ("refund-called", refundCalled, null), ("refund-event", refundEvent, null))]
                :
                [
                    new Sample("refund", 1, 1000, ("refund-called", refundCalled, null), ("refund-event", refundEvent, null)),
                    new Sample("plain", 1, 1000, ("plain-called", true, null)),
                ]
        );

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

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        _ = database.Pool;
        return await app.CreateClient().GetAsync(new Uri(url, UriKind.Relative), Cancellation);
    }

    [Fact]
    public async Task Runs_named_one_by_one_are_columns_with_a_cell_per_scenario_and_a_rate_per_tag_and_no_baseline()
    {
        var agent = await AgentAsync(database);
        var weak = await VariantAsync(agent, null, refundCalled: true, refundEvent: false);
        var strong = await VariantAsync(agent, null, refundCalled: true, refundEvent: true, onlyRefund: true);

        var comparison = await TestServer.Read<RunComparison>(await GetAsync($"/runs/compare?runs={weak}&runs={strong}"));

        Assert.Equal((agent.Name, null), (comparison.Agent, comparison.Group));
        Assert.Null(comparison.Baseline);
        Assert.Equal([weak, strong], comparison.Runs.Select(run => run.Run.Id));
        Assert.Equal([1000.0, 1000.0], comparison.Runs.Select(run => run.MedianMs));
        Assert.Equal(["plain", "refund"], comparison.Scenarios.Select(scenario => scenario.Key));
        var refund = comparison.Scenarios.Single(scenario => scenario.Key == "refund");
        Assert.Equal(["billing"], refund.Tags);
        Assert.Equal((1, 2), (refund.Cells[0]!.ChecksPassed, refund.Cells[0]!.ChecksTotal));
        Assert.Equal(["refund-event"], refund.Cells[0]!.FailedChecks);
        var failing = refund.Cells[0]!.Checks.Single(check => check.Key == "refund-event");
        Assert.Equal((false, 0, 1), (failing.Passed, failing.SamplesPassed, failing.Samples));
        Assert.Equal([(1, "m")], failing.Failures.Select(failure => (failure.Sample, failure.Message)));
        Assert.Empty(refund.Cells[1]!.Checks.Single(check => check.Key == "refund-event").Failures);
        Assert.Equal((2, 2), (refund.Cells[1]!.ChecksPassed, refund.Cells[1]!.ChecksTotal));
        Assert.Null(comparison.Scenarios.Single(scenario => scenario.Key == "plain").Cells[1]);
        Assert.Equal(
            ["billing|0.5|1"],
            comparison.Tags.Where(row => row.Tag == "billing").Select(row => $"{row.Tag}|{row.Rates[0].PassRate}|{row.Rates[1].PassRate}")
        );
        Assert.All(comparison.Tags.SelectMany(row => row.Rates), rate => Assert.Null(rate.Change));
    }

    [Fact]
    public async Task A_baseline_adds_the_changes_against_that_run_and_the_columns_keep_their_order()
    {
        var agent = await AgentAsync(database);
        var first = await RunAsync(
            agent,
            new Sample("refund", 1, 1000, ("refund-called", true, null), ("refund-event", false, "judge-a")),
            new Sample("plain", 1, 3000, ("plain-called", true, null))
        );
        var second = await RunAsync(
            agent,
            new Sample("refund", 1, 500, ("refund-called", true, null), ("refund-event", true, "judge-b")),
            new Sample("plain", 1, 3000, ("plain-called", false, null))
        );

        var comparison = await TestServer.Read<RunComparison>(await GetAsync($"/runs/compare?runs={second}&runs={first}&baseline={first}"));

        Assert.Equal([second, first], comparison.Runs.Select(run => run.Run.Id));
        var baseline = comparison.Baseline!;
        Assert.Equal(first, baseline.RunId);
        Assert.Equal([second, first], baseline.Runs.Select(run => run.Run.Id));
        Assert.Equal(["judge-b"], baseline.Runs[0].Run.JudgeModels);
        Assert.Equal(["judge-a"], baseline.Runs[1].Run.JudgeModels);
        Assert.Equal([null, null], baseline.Runs.Select(run => run.JudgeModel));
        Assert.Equal([1750.0, 2000.0], baseline.Runs.Select(run => run.MedianMs));
        Assert.Equal(new TimeChange(-250, Contracts.Runs.Trend.Better), baseline.Runs[0].MedianChange);
        Assert.Null(baseline.Runs[1].MedianChange);
        Assert.Equal(
            ["|0.6666666666666666|0.6666666666666666", "billing|1|0.5"],
            baseline.Tags.Select(row => $"{row.Tag}|{row.Rates[0].PassRate:R}|{row.Rates[1].PassRate:R}")
        );
        Assert.Equal(new RateChange(50, Contracts.Runs.Trend.Better), baseline.Tags.Single(row => row.Tag == "billing").Rates[0].Change);
        Assert.Equal(
            ["plain/plain-called|True|True", "refund/refund-called|False|True", "refund/refund-event|True|True"],
            baseline
                .Checks.OrderBy(row => row.CheckKey, StringComparer.Ordinal)
                .Select(row => $"{row.ScenarioKey}/{row.CheckKey}|{row.Differs}|{row.Comparable}")
        );
        var eventRow = baseline.Checks.Single(row => row.CheckKey == "refund-event");
        Assert.Equal([true, false], eventRow.Cells.Select(cell => cell?.Passed));
        Assert.Equal([Contracts.Runs.Trend.Better, Contracts.Runs.Trend.Same], eventRow.Cells.Select(cell => cell?.Trend));
        Assert.Equal("refund-event", eventRow.Definition.Key);
    }

    [Fact]
    public async Task A_group_is_its_runs_oldest_first_and_only_the_agents_that_ask_for_it()
    {
        var agent = await AgentAsync(database);
        var other = await AgentAsync(database);
        var first = await VariantAsync(agent, "sweep-1", true, true);
        var second = await VariantAsync(agent, "sweep-1", true, false);
        await VariantAsync(agent, "sweep-2", true, true);

        var comparison = await TestServer.Read<RunComparison>(await GetAsync($"/runs/compare?group=sweep-1&agent={agent.Name}"));
        await VariantAsync(other, "sweep-1", true, true);
        var several = await GetAsync("/runs/compare?group=sweep-1");

        Assert.Equal("sweep-1", comparison.Group);
        Assert.Equal([first, second], comparison.Runs.Select(run => run.Run.Id));
        Assert.Null(comparison.Baseline);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, several.StatusCode);
        Assert.Equal(["group"], await TestServer.ErrorFields(several));
    }

    [Fact]
    public async Task A_baseline_works_for_a_group_too()
    {
        var agent = await AgentAsync(database);
        var first = await VariantAsync(agent, "sweep-1", true, true);
        var second = await VariantAsync(agent, "sweep-1", true, false);

        var comparison = await TestServer.Read<RunComparison>(
            await GetAsync($"/runs/compare?group=sweep-1&agent={agent.Name}&baseline={second}")
        );

        Assert.Equal([first, second], comparison.Runs.Select(run => run.Run.Id));
        Assert.Equal(second, comparison.Baseline!.RunId);
        Assert.Equal([first, second], comparison.Baseline.Runs.Select(run => run.Run.Id));
    }

    [Fact]
    public async Task A_baseline_outside_the_runs_or_with_only_one_run_is_refused()
    {
        var agent = await AgentAsync(database);
        var one = await VariantAsync(agent, null, true, true);
        var two = await VariantAsync(agent, null, true, true);
        var outside = await VariantAsync(agent, null, true, true);

        var notAmong = await GetAsync($"/runs/compare?runs={one}&runs={two}&baseline={outside}");
        var alone = await GetAsync($"/runs/compare?runs={one}&baseline={one}");
        var aloneOutside = await GetAsync($"/runs/compare?runs={one}&baseline={two}");
        var fine = await GetAsync($"/runs/compare?runs={one}&runs={two}&baseline={two}");

        Assert.Equal(["baseline"], await TestServer.ErrorFields(notAmong));
        Assert.Equal(["baseline"], await TestServer.ErrorFields(alone));
        Assert.Equal(["baseline"], await TestServer.ErrorFields(aloneOutside));
        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
    }

    [Fact]
    public async Task Twenty_runs_can_be_compared_but_not_twenty_one()
    {
        var agent = await AgentAsync(database);
        var run = await VariantAsync(agent, null, true, true);
        var ids = new List<Guid> { run };
        for (var index = 1; index < 20; index++)
        {
            ids.Add(await VariantAsync(agent, null, true, true));
        }

        var twenty = await GetAsync($"/runs/compare?{string.Join("&", ids.Select(id => $"runs={id}"))}&baseline={ids[7]}");
        var tooMany = await GetAsync($"/runs/compare?{string.Join("&", ids.Select(id => $"runs={id}"))}&runs={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, twenty.StatusCode);
        Assert.Equal(ids[7], (await TestServer.Read<RunComparison>(twenty)).Baseline!.RunId);
        Assert.Equal(["runs"], await TestServer.ErrorFields(tooMany));
    }

    [Fact]
    public async Task Comparing_refuses_runs_of_different_agents_both_or_neither_selection_and_knows_no_unknown_run_or_group()
    {
        var agent = await AgentAsync(database);
        var other = await AgentAsync(database);
        var one = await VariantAsync(agent, null, true, true);
        var foreign = await VariantAsync(other, null, true, true);

        var differentAgents = await GetAsync($"/runs/compare?runs={one}&runs={foreign}");
        var both = await GetAsync($"/runs/compare?runs={one}&group=sweep-1");
        var neither = await GetAsync("/runs/compare");
        var unknownRun = await GetAsync($"/runs/compare?runs={Guid.NewGuid()}");
        var unknownGroup = await GetAsync($"/runs/compare?group=no-such-group&agent={agent.Name}");
        var alone = await GetAsync($"/runs/compare?runs={one}");

        Assert.Equal(["runs"], await TestServer.ErrorFields(differentAgents));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, both.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, neither.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownRun.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownGroup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, alone.StatusCode);
    }

    [Fact]
    public async Task A_run_summary_names_the_distinct_judge_models_of_its_verdicts_sorted()
    {
        var agent = await AgentAsync(database);
        var judged = await RunAsync(
            agent,
            new Sample("refund", 1, 1, ("refund-called", true, null), ("refund-event", true, "zeta")),
            new Sample("refund", 2, 1, ("refund-called", true, null), ("refund-event", true, "alpha")),
            new Sample("plain", 1, 1, ("plain-called", true, "zeta"))
        );
        var plain = await RunAsync(agent, new Sample("plain", 1, 1, ("plain-called", true, null)));

        var summary = await TestServer.Read<RunSummary>(await GetAsync($"/runs/{judged}"));
        var none = await TestServer.Read<RunSummary>(await GetAsync($"/runs/{plain}"));
        var listed = await TestServer.Read<RunList>(await GetAsync($"/runs?agent={agent.Name}"));

        Assert.Equal(["alpha", "zeta"], summary.JudgeModels);
        Assert.Empty(none.JudgeModels);
        Assert.Equal(["alpha", "zeta"], listed.Runs.Single(run => run.Id == judged).JudgeModels);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
