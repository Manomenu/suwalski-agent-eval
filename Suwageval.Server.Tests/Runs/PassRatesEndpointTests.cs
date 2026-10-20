using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Tests.Runs;

/// <summary>The pass-rate trend endpoint over a realistic history: its numbers are pinned, whatever way they are computed.</summary>
public sealed class PassRatesEndpointTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>One stored sample: the scenario, its number, the duration (null: the agent made no call) and the verdicts by check key.</summary>
    private sealed record Sample(string Scenario, int Number, int? DurationMs, params (string Check, bool Passed)[] Verdicts);

    private async Task<Guid> RunAsync(
        SeededAgent agent,
        string? tag,
        RunStatus status,
        IReadOnlyList<Sample> samples,
        Guid? recheckOf = null
    )
    {
        var store = app.Services.GetRequiredService<RunStore>();
        var scenarios = app.Services.GetRequiredService<ScenarioStore>();
        var checks = await app.Services.GetRequiredService<CheckStore>().ListAsync(agent.Id, cancellationToken: Cancellation);
        var id = await store.EnqueueAsync(agent.Id, agent.VersionId, tag, agent.EndpointUrl, recheckOf, cancellationToken: Cancellation);
        foreach (var sample in samples)
        {
            var scenario = await scenarios.GetAsync(agent.Id, sample.Scenario, Cancellation);
            var answered = sample.Verdicts.Length > 0;
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    sample.Number,
                    answered ? SampleStatus.Done : SampleStatus.Failed,
                    answered ? "[]" : null,
                    answered ? null : "agent down",
                    sample.DurationMs,
                    [],
                    [
                        .. sample.Verdicts.Select(verdict => new NewCheckResult(
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
        if (status != RunStatus.Queued)
        {
            await store.FinishAsync(id, status, cancellationToken: Cancellation);
        }
        return id;
    }

    [Fact]
    public async Task The_trend_over_a_history_with_tags_rechecks_errors_and_missing_durations_is_pinned()
    {
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [
                SeededAgent.Scenario("refund", ["regression"]),
                SeededAgent.Scenario("other", ["smoke", "regression"]),
                SeededAgent.Scenario("plain"),
            ],
            [
                SeededAgent.RefundAfterLookup("refund-called", "refund"),
                SeededAgent.RefundedEvent("refund-event", "refund"),
                SeededAgent.RefundAfterLookup("other-called", "other", 0.5),
                SeededAgent.RefundAfterLookup("plain-called", "plain"),
            ]
        );
        var run1 = await RunAsync(
            agent,
            null,
            RunStatus.Done,
            [
                new Sample("refund", 1, 1000, ("refund-called", true), ("refund-event", false)),
                new Sample("refund", 2, 3000, ("refund-called", true), ("refund-event", true)),
                new Sample("other", 1, 500, ("other-called", true)),
                new Sample("other", 2, 800, ("other-called", false)),
                new Sample("other", 3, null),
                new Sample("plain", 1, 9000, ("plain-called", false)),
            ]
        );
        var run2 = await RunAsync(
            agent,
            "regression",
            RunStatus.Done,
            [new Sample("refund", 1, 2000, ("refund-called", true), ("refund-event", true))]
        );
        await RunAsync(agent, null, RunStatus.Failed, [new Sample("refund", 1, 100, ("refund-called", true))]);
        await RunAsync(agent, null, RunStatus.Done, [new Sample("refund", 1, null)]);
        var run5 = await RunAsync(
            agent,
            null,
            RunStatus.Done,
            [
                new Sample("refund", 1, 4000, ("refund-called", false), ("refund-event", false)),
                new Sample("other", 1, 6000, ("other-called", false)),
            ]
        );
        await RunAsync(agent, null, RunStatus.Done, [new Sample("refund", 1, 7000, ("refund-called", false))], recheckOf: run5);
        var newestRecheck = await RunAsync(
            agent,
            null,
            RunStatus.Done,
            [
                new Sample("refund", 1, 4500, ("refund-called", true), ("refund-event", true)),
                new Sample("other", 1, 6500, ("other-called", true)),
            ],
            recheckOf: run5
        );
        await RunAsync(agent, null, RunStatus.Queued, []);
        var run8 = await RunAsync(agent, null, RunStatus.Done, [new Sample("other", 1, 1200, ("other-called", true))]);

        var points = await Client()
            .GetFromJsonAsync<List<RunPassRates>>(
                new Uri($"/agents/{agent.Name}/pass-rates", UriKind.Relative),
                ContractJson.Options,
                Cancellation
            );

        Assert.NotNull(points);
        Assert.Equal([run1, run2, run5, run8], points.Select(point => point.RunId));
        Assert.Equal([null, null, newestRecheck, null], points.Select(point => point.RecheckedBy));
        Assert.Equal(
            [
                "|0.5|1000|9000|regression:0.5:1000:9000,smoke:1:650:800",
                "regression|1|2000|2000|regression:1:2000:2000",
                "|1|5500|6500|regression:1:5500:6500,smoke:1:6500:6500",
                "|1|1200|1200|regression:1:1200:1200,smoke:1:1200:1200",
            ],
            points.Select(point =>
                $"{point.Tag}|{point.PassRate:R}|{point.MedianMs:R}|{point.P95Ms:R}|"
                + string.Join(",", point.Tags.Select(tag => $"{tag.Tag}:{tag.PassRate:R}:{tag.MedianMs:R}:{tag.P95Ms:R}"))
            )
        );
    }

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
