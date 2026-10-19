using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Runs;

/// <summary>The comparison of runs and the judge models of a run's summary, over runs stored in the real database.</summary>
public sealed class RunCompareEndpointTests(TestDatabase database) : IAsyncDisposable
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

    private async Task<Guid> RunAsync(SeededAgent agent, params Sample[] samples)
    {
        var store = app.Services.GetRequiredService<RunStore>();
        var scenarios = app.Services.GetRequiredService<ScenarioStore>();
        var checks = await app.Services.GetRequiredService<CheckStore>().ListAsync(agent.Id, cancellationToken: Cancellation);
        var id = await store.EnqueueAsync(agent.Id, agent.VersionId, null, agent.EndpointUrl, cancellationToken: Cancellation);
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
    public async Task Runs_are_compared_against_the_first_one()
    {
        var agent = await AgentAsync(database);
        var baseline = await RunAsync(
            agent,
            new Sample("refund", 1, 1000, ("refund-called", true, null), ("refund-event", false, "judge-a")),
            new Sample("plain", 1, 3000, ("plain-called", true, null))
        );
        var better = await RunAsync(
            agent,
            new Sample("refund", 1, 500, ("refund-called", true, null), ("refund-event", true, "judge-b")),
            new Sample("plain", 1, 3000, ("plain-called", false, null))
        );

        var response = await GetAsync($"/runs/compare?runs={baseline}&runs={better}");
        var comparison = await TestServer.Read<RunComparison>(response);

        Assert.Equal([baseline, better], comparison.Runs.Select(run => run.Run.Id));
        Assert.Equal(["judge-a"], comparison.Runs[0].Run.JudgeModels);
        Assert.Equal(["judge-b"], comparison.Runs[1].Run.JudgeModels);
        Assert.Equal([null, null], comparison.Runs.Select(run => run.JudgeModel));
        Assert.Equal([2000.0, 1750.0], comparison.Runs.Select(run => run.MedianMs));
        Assert.Equal(new TimeChange(-250, Trend.Better), comparison.Runs[1].MedianChange);
        Assert.Equal(
            ["|0.6666666666666666|0.6666666666666666", "billing|0.5|1"],
            comparison.Tags.Select(row => $"{row.Tag}|{row.Rates[0].PassRate:R}|{row.Rates[1].PassRate:R}")
        );
        Assert.Equal(
            ["plain/plain-called|True|True", "refund/refund-called|False|True", "refund/refund-event|True|True"],
            comparison
                .Checks.OrderBy(row => row.CheckKey, StringComparer.Ordinal)
                .Select(row => $"{row.ScenarioKey}/{row.CheckKey}|{row.Differs}|{row.Comparable}")
        );
        var eventRow = comparison.Checks.Single(row => row.CheckKey == "refund-event");
        Assert.Equal([false, true], eventRow.Cells.Select(cell => cell?.Passed));
        Assert.Equal([Trend.Same, Trend.Better], eventRow.Cells.Select(cell => cell?.Trend));
        Assert.Equal("refund-event", eventRow.Definition.Key);
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

    [Fact]
    public async Task Comparing_refuses_a_wrong_number_of_runs_and_runs_of_different_agents_and_knows_no_unknown_run()
    {
        var agent = await AgentAsync(database);
        var other = await AgentAsync(database);
        var one = await RunAsync(agent, new Sample("plain", 1, 1, ("plain-called", true, null)));
        var two = await RunAsync(agent, new Sample("plain", 1, 1, ("plain-called", true, null)));
        var foreign = await RunAsync(other, new Sample("plain", 1, 1, ("plain-called", true, null)));

        var alone = await GetAsync($"/runs/compare?runs={one}");
        var twice = await GetAsync($"/runs/compare?runs={one}&runs={one}");
        var six = await GetAsync($"/runs/compare?{string.Join("&", Enumerable.Range(0, 6).Select(_ => $"runs={Guid.NewGuid()}"))}");
        var differentAgents = await GetAsync($"/runs/compare?runs={one}&runs={foreign}");
        var unknown = await GetAsync($"/runs/compare?runs={one}&runs={Guid.NewGuid()}");
        var fine = await GetAsync($"/runs/compare?runs={two}&runs={one}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, alone.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, twice.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, six.StatusCode);
        Assert.Equal(["runs"], await TestServer.ErrorFields(differentAgents));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
