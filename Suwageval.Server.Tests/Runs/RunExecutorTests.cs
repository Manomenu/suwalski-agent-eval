using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Invocation;
using Suwageval.Server.Judges;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Judges;

namespace Suwageval.Server.Tests.Runs;

/// <summary>The worker's job end to end, against a real PostgreSQL and a fake agent over real HTTP — no model.</summary>
public sealed class RunExecutorTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RunStore Store() => new(database.Pool);

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The client lives as long as the test process."
    )]
    private async Task<(RunRow Run, RunExecutor Executor)> ClaimAsync(
        SeededAgent agent,
        string? tag = null,
        int parallel = 2,
        Uri? endpoint = null,
        Judge? judge = null
    )
    {
        var store = Store();
        await store.EnqueueAsync(agent.Id, agent.VersionId, tag, (endpoint ?? new Uri(agent.EndpointUrl)).AbsoluteUri, Cancellation);
        var run = await store.ClaimNextAsync(agent.Id, Cancellation);
        var executor = new RunExecutor(
            store,
            new ScenarioStore(database.Pool),
            new CheckStore(database.Pool),
            new AgentStore(database.Pool),
            new AgentClient(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }),
            judge ?? RecordedModel.JudgeAnswering("passed.json"),
            Options.Create(new RunOptions { MaxParallelScenarios = parallel }),
            NullLogger<RunExecutor>.Instance
        );
        return (run ?? throw new InvalidOperationException("nothing was queued"), executor);
    }

    private async Task<RunResults> RunAsync(SeededAgent agent, string? tag = null, int parallel = 2, Judge? judge = null)
    {
        var (run, executor) = await ClaimAsync(agent, tag, parallel, judge: judge);
        await executor.ExecuteAsync(run, Cancellation);
        return await ResultsAsync(run.Id);
    }

    private async Task<RunResults> ResultsAsync(Guid runId)
    {
        var store = Store();
        var run = await store.GetAsync(runId, Cancellation) ?? throw new InvalidOperationException("no such run");
        return RunReport.Results(
            run,
            await store.ListScenarioResultsAsync(runId, Cancellation),
            await store.ListCheckResultsAsync(runId, Cancellation)
        );
    }

    [Fact]
    public async Task A_run_calls_the_agent_stores_its_output_and_judges_every_check()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        Assert.Equal((1, 1, 2, 0), (results.Run.SamplesTotal, results.Run.SamplesDone, results.Run.ChecksPassed, results.Run.ChecksFailed));
        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal("refund", scenario.Key);
        var sample = Assert.Single(scenario.Samples);
        Assert.Equal(SampleStatus.Done, sample.Status);
        Assert.Equal(6, sample.Output?.Count);
        Assert.All(scenario.Checks, check => Assert.True(check.Passed));
        Assert.Equal(["refund-called", "refund-event"], scenario.Checks.Select(check => check.Key));
        var called = scenario.Checks[0];
        Assert.Equal(1.0, called.PassRate);
        Assert.Equal("refund_order", Assert.IsType<ToolCall>(Assert.Single(called.Samples[0].Evidence)).Name);
        var request = Assert.Single(fake.Requests);
        Assert.Contains("Please refund order A-17.", request, StringComparison.Ordinal);
    }

    private static JudgeCheck PoliteRefusal(string key, string scenarioKey) =>
        new()
        {
            Key = key,
            ScenarioKey = scenarioKey,
            Criteria = "The assistant refuses to refund an order older than 30 days and explains why, politely.",
        };

    [Fact]
    public async Task A_judge_check_gets_the_verdict_of_the_model_and_its_reason_is_the_message()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [PoliteRefusal("polite-refusal", "refund"), SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var results = await RunAsync(agent, judge: RecordedModel.JudgeAnswering("failed.json"));

        Assert.Equal(RunStatus.Done, results.Run.Status);
        var checks = Assert.Single(results.Scenarios).Checks.ToDictionary(check => check.Key);
        var judged = Assert.Single(checks["polite-refusal"].Samples);
        Assert.False(checks["polite-refusal"].Passed);
        Assert.False(judged.Passed);
        Assert.False(judged.NotJudged);
        Assert.Contains("did not refuse", judged.Message, StringComparison.Ordinal);
        Assert.True(checks["refund-called"].Passed);
    }

    [Fact]
    public async Task A_judge_that_cannot_judge_is_a_not_judged_failure_of_that_check_and_the_run_goes_on()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: 2)],
            [PoliteRefusal("polite-refusal", "refund"), SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var results = await RunAsync(agent, judge: RecordedModel.JudgeFailing(new HttpRequestException("connection refused")));

        Assert.Equal(RunStatus.Done, results.Run.Status);
        Assert.Equal((2, 2, 2), (results.Run.SamplesTotal, results.Run.SamplesDone, results.Run.ChecksPassed));
        Assert.Equal(2, results.Run.ChecksFailed);
        var checks = Assert.Single(results.Scenarios).Checks.ToDictionary(check => check.Key);
        Assert.All(
            checks["polite-refusal"].Samples,
            sample =>
            {
                Assert.False(sample.Passed);
                Assert.True(sample.NotJudged);
                Assert.Contains("connection refused", sample.Message, StringComparison.Ordinal);
            }
        );
        Assert.All(checks["refund-called"].Samples, sample => Assert.False(sample.NotJudged));
        Assert.True(checks["refund-called"].Passed);
    }

    private const string UndeclaredTurn = /*lang=json,strict*/
        """
        {"items":[
          {"kind":"ToolCall","id":"c1","name":"cancel_order","arguments":{}},
          {"kind":"Event","type":"order.cancelled","data":{}},
          {"kind":"ToolCall","id":"c2","name":"get_order","arguments":{}},
          {"kind":"AssistantMessage","text":"Done."}
        ]}
        """;

    [Fact]
    public async Task A_tool_call_or_event_the_version_does_not_declare_is_a_warning_on_the_sample_not_a_failure()
    {
        await using var fake = await FakeAgent.AlwaysAsync(UndeclaredTurn);
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("refund")], []);

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        var sample = Assert.Single(Assert.Single(results.Scenarios).Samples);
        Assert.Equal(SampleStatus.Done, sample.Status);
        Assert.Equal(["tool 'cancel_order'", "event 'order.cancelled'"], sample.Undeclared);
    }

    [Fact]
    public async Task A_contract_that_is_still_the_same_at_the_end_is_not_flagged()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("refund")], []);

        var results = await RunAsync(agent);

        Assert.False(results.Run.ContractChangedDuringRun);
        Assert.Equal(1, fake.ContractRequests);
    }

    [Fact]
    public async Task A_contract_that_changed_while_the_run_was_going_is_flagged_on_the_run()
    {
        // A redeploy between the samples: the agent serves another contract once its first turn has been answered.
        FakeAgent? redeployed = null;
        await using var fake = await FakeAgent.StartAsync(
            (_, _) =>
            {
                redeployed?.ServeContract(SeededAgent.ExampleVersion with { SystemPrompt = "Redeployed mid-run." });
                return Results.Text(FakeAgent.RefundTurn, "application/json");
            }
        );
        redeployed = fake;
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("refund")], []);

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        Assert.True(results.Run.ContractChangedDuringRun);
    }

    [Fact]
    public async Task An_agent_that_cannot_be_asked_again_at_the_end_does_not_flag_a_change()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        fake.ServeContract(_ => Results.StatusCode(500));
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("refund")], []);

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        Assert.False(results.Run.ContractChangedDuringRun);
    }

    [Fact]
    public async Task A_failing_check_is_reported_with_its_message_and_the_run_is_still_done()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        var check = Assert.Single(Assert.Single(results.Scenarios).Checks);
        Assert.False(check.Passed);
        Assert.Equal(0.0, check.PassRate);
        Assert.Contains("refund_order was not called", check.Samples[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.8, true)]
    [InlineData(1.0, false)]
    public async Task The_pass_rate_over_samples_is_held_against_the_threshold(double threshold, bool passes)
    {
        // Four of five samples refund; the third answers plainly.
        await using var fake = await FakeAgent.StartAsync(
            (_, _) => Results.Text(Interlocked.Increment(ref calls) == 3 ? FakeAgent.PlainTurn : FakeAgent.RefundTurn, "application/json")
        );
        calls = 0;
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: 5)],
            [SeededAgent.RefundAfterLookup("refund-called", "refund", threshold)]
        );

        var results = await RunAsync(agent, parallel: 1);

        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal([1, 2, 3, 4, 5], scenario.Samples.Select(sample => sample.Sample));
        var check = Assert.Single(scenario.Checks);
        Assert.Equal(0.8, check.PassRate, 6);
        Assert.Equal(threshold, check.PassThreshold);
        Assert.Equal(passes, check.Passed);
        Assert.Equal(5, results.Run.SamplesTotal);
    }

    private int calls;

    [Fact]
    public async Task An_agent_that_fails_gives_failed_samples_and_failed_checks_but_not_a_failed_run()
    {
        await using var fake = await FakeAgent.StartAsync((_, _) => Results.Text("boom", statusCode: 500));
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        var scenario = Assert.Single(results.Scenarios);
        var sample = Assert.Single(scenario.Samples);
        Assert.Equal(SampleStatus.Failed, sample.Status);
        Assert.Null(sample.Output);
        Assert.Contains("500", sample.Error, StringComparison.Ordinal);
        var check = Assert.Single(scenario.Checks);
        Assert.False(check.Passed);
        Assert.Contains("Not evaluated", check.Samples[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_agent_that_is_not_there_is_a_failed_sample_too()
    {
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            "http://127.0.0.1:1",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var results = await RunAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        Assert.Contains("could not be reached", Assert.Single(Assert.Single(results.Scenarios).Samples).Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task More_than_one_turn_is_recorded_as_not_supported_yet_without_calling_the_agent()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("multi", turns: 2)],
            [SeededAgent.RefundAfterLookup("refund-called", "multi")]
        );

        var results = await RunAsync(agent);

        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal(SampleStatus.Failed, scenario.Samples[0].Status);
        Assert.Contains("not supported yet", scenario.Samples[0].Error, StringComparison.Ordinal);
        Assert.False(Assert.Single(scenario.Checks).Passed);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task A_tag_picks_the_scenarios_and_without_one_all_run()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [
                SeededAgent.Scenario("tagged", ["regression"]),
                SeededAgent.Scenario("other", ["smoke"]),
                SeededAgent.Scenario("both", ["regression", "smoke"]),
            ],
            []
        );

        var tagged = await RunAsync(agent, "regression");
        var all = await RunAsync(agent);

        Assert.Equal(["both", "tagged"], tagged.Scenarios.Select(scenario => scenario.Key));
        Assert.Equal(["both", "other", "tagged"], all.Scenarios.Select(scenario => scenario.Key));
    }

    [Fact]
    public async Task A_scenario_without_checks_still_shows_its_output()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("greeting")], []);

        var scenario = Assert.Single((await RunAsync(agent)).Scenarios);

        Assert.Empty(scenario.Checks);
        Assert.Equal("Hello, how can I help?", Assert.IsType<AssistantMessage>(Assert.Single(scenario.Samples[0].Output ?? [])).Text);
    }

    [Fact]
    public async Task Samples_run_in_parallel_up_to_the_configured_degree()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn, TimeSpan.FromMilliseconds(250));
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("a"), SeededAgent.Scenario("b"), SeededAgent.Scenario("c"), SeededAgent.Scenario("d", samples: 2)],
            []
        );

        var results = await RunAsync(agent, parallel: 2);

        Assert.Equal(5, results.Run.SamplesDone);
        Assert.Equal(2, fake.MaxConcurrency);
    }

    [Fact]
    public async Task One_at_a_time_when_the_degree_is_one()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn, TimeSpan.FromMilliseconds(100));
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("a"), SeededAgent.Scenario("b"), SeededAgent.Scenario("c")],
            []
        );

        await RunAsync(agent, parallel: 1);

        Assert.Equal(1, fake.MaxConcurrency);
    }

    [Fact]
    public async Task A_cancel_requested_before_the_next_sample_stops_the_run()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("a"), SeededAgent.Scenario("b")],
            []
        );
        var (run, executor) = await ClaimAsync(agent);
        await Store().RequestCancelAsync(run.Id, Cancellation);

        await executor.ExecuteAsync(run, Cancellation);

        var results = await ResultsAsync(run.Id);
        Assert.Equal(RunStatus.Cancelled, results.Run.Status);
        Assert.Empty(results.Scenarios);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task A_cancel_during_the_run_lets_the_samples_in_flight_finish_and_starts_no_new_one()
    {
        RunStore? store = null;
        var runId = Guid.Empty;
        await using var fake = await FakeAgent.StartAsync(
            (_, call) =>
            {
                if (call == 1)
                {
                    store?.RequestCancelAsync(runId, Cancellation).GetAwaiter().GetResult();
                }
                return Results.Text(FakeAgent.PlainTurn, "application/json");
            }
        );
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [.. Enumerable.Range(1, 6).Select(n => SeededAgent.Scenario($"s{n}"))],
            []
        );
        var (run, executor) = await ClaimAsync(agent, parallel: 1);
        store = Store();
        runId = run.Id;

        await executor.ExecuteAsync(run, Cancellation);

        var results = await ResultsAsync(run.Id);
        Assert.Equal(RunStatus.Cancelled, results.Run.Status);
        Assert.Equal(1, results.Run.SamplesDone);
        Assert.Equal(6, results.Run.SamplesTotal);
    }

    [Fact]
    public async Task Unreadable_stored_content_fails_the_run_with_the_reason()
    {
        var agent = await SeededAgent.CreateAsync(database.Pool, "http://127.0.0.1:1", [], []);
        await new ScenarioStore(database.Pool).UpsertAsync(agent.Id, [new ScenarioItem("broken", ["regression"], "{}")], Cancellation);
        var (run, executor) = await ClaimAsync(agent);

        await executor.ExecuteAsync(run, Cancellation);

        var failed = await Store().GetAsync(run.Id, Cancellation);
        Assert.Equal(RunStatus.Failed, failed?.State);
        Assert.NotNull(failed?.Error);
        Assert.NotNull(failed?.FinishedAt);
    }
}
