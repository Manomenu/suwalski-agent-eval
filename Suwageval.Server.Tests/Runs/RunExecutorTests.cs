using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Invocation;
using Suwageval.Server.Judges;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Simulation;
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
        IReadOnlyCollection<string>? tags = null,
        int parallel = 2,
        Uri? endpoint = null,
        Judge? judge = null,
        ArgumentJudge? argumentJudge = null,
        UserSimulator? simulator = null
    )
    {
        var store = Store();
        await store.EnqueueAsync(
            agent.Id,
            agent.VersionId,
            tags ?? [],
            (endpoint ?? new Uri(agent.EndpointUrl)).AbsoluteUri,
            cancellationToken: Cancellation
        );
        var run = await store.ClaimNextAsync(agent.Id, Cancellation);
        var executor = new RunExecutor(
            store,
            new ScenarioStore(database.Pool),
            new CheckStore(database.Pool),
            new AgentStore(database.Pool),
            new AgentClient(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }),
            simulator ?? RecordedModel.SimulatorAnswering("simulated-end.json"),
            judge ?? RecordedModel.JudgeAnswering("passed.json"),
            argumentJudge ?? RecordedModel.Answering("passed.json").ArgumentJudge(),
            Options.Create(new RunOptions { MaxParallelScenarios = parallel }),
            NullLogger<RunExecutor>.Instance
        );
        return (run ?? throw new InvalidOperationException("nothing was queued"), executor);
    }

    private async Task<RunResults> RunAsync(
        SeededAgent agent,
        IReadOnlyCollection<string>? tags = null,
        int parallel = 2,
        Judge? judge = null
    )
    {
        var (run, executor) = await ClaimAsync(agent, tags, parallel, judge: judge);
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

    [Fact]
    public async Task The_scenarios_fixtures_and_the_correlation_headers_reach_the_agent_on_every_turn()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        fake.ServeContract(SeededAgent.ExampleVersionWithFixtures);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [
                SeededAgent.Scenario(
                    "with-fixtures",
                    samples: 2,
                    fixtures: JsonDocument
                        .Parse( /*lang=json,strict*/
                            """{"pinnedResources":["plans"]}"""
                        )
                        .RootElement.Clone()
                ),
                SeededAgent.Scenario("without-fixtures"),
            ],
            [],
            SeededAgent.ExampleVersionWithFixtures
        );
        var (run, executor) = await ClaimAsync(agent, parallel: 1);

        await executor.ExecuteAsync(run, Cancellation);

        var turns = fake
            .Requests.Zip(fake.RequestHeaders, (body, headers) => (Body: JsonDocument.Parse(body).RootElement, Headers: headers))
            .ToList();
        Assert.Equal(3, turns.Count);
        var baggage = turns.Select(turn => (turn.Body, Baggage: Baggage(turn.Headers))).ToList();
        Assert.All(baggage, turn => Assert.Equal(run.Id.ToString(), turn.Baggage[SuwagevalBaggage.RunId]));
        Assert.All(baggage, turn => Assert.Equal(run.AgentVersionId.ToString(), turn.Baggage[SuwagevalBaggage.AgentVersionId]));
        Assert.All(baggage, turn => Assert.Equal("Turn", turn.Baggage[SuwagevalBaggage.ScenarioTypeKey]));
        Assert.All(baggage, turn => Assert.Equal("regression", turn.Baggage[SuwagevalBaggage.ScenarioTags]));
        Assert.All(baggage, turn => Assert.DoesNotContain(SuwagevalBaggage.RunTags, turn.Baggage.Keys));
        Assert.All(baggage, turn => Assert.DoesNotContain(SuwagevalBaggage.RunGroup, turn.Baggage.Keys));
        Assert.Equal(3, baggage.Select(turn => turn.Body.GetProperty("conversationId").GetString()).Distinct().Count());
        var withFixtures = baggage.Where(turn => turn.Baggage[SuwagevalBaggage.ScenarioKey] == "with-fixtures").ToList();
        Assert.Equal(["1", "2"], withFixtures.Select(turn => turn.Baggage[SuwagevalBaggage.ScenarioSample]).Order());
        Assert.All(
            withFixtures,
            turn =>
                Assert.Equal( /*lang=json,strict*/
                    """{"pinnedResources":["plans"]}""",
                    turn.Body.GetProperty("fixtures").GetRawText()
                )
        );
        var (withoutBody, _) = Assert.Single(baggage, turn => turn.Baggage[SuwagevalBaggage.ScenarioKey] == "without-fixtures");
        Assert.False(withoutBody.TryGetProperty("fixtures", out _));
    }

    [Fact]
    public async Task The_duration_of_the_call_is_stored_per_sample_and_missing_when_no_call_was_made()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn, TimeSpan.FromMilliseconds(80));
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("answered"), SeededAgent.Scenario("multi", turns: 2)],
            []
        );

        var results = await RunAsync(agent);

        var answered = Assert.Single(Assert.Single(results.Scenarios, scenario => scenario.Key == "answered").Samples);
        Assert.True(answered.DurationMs >= 70, $"{answered.DurationMs}");
        var skipped = Assert.Single(Assert.Single(results.Scenarios, scenario => scenario.Key == "multi").Samples);
        Assert.Null(skipped.DurationMs);
    }

    [Fact]
    public async Task A_call_that_failed_still_has_a_duration()
    {
        await using var fake = await FakeAgent.StartAsync((_, _) => Results.StatusCode(500), TimeSpan.FromMilliseconds(80));
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("down")], []);

        var results = await RunAsync(agent);

        var sample = Assert.Single(Assert.Single(results.Scenarios).Samples);
        Assert.Equal(SampleStatus.Failed, sample.Status);
        Assert.True(sample.DurationMs >= 70, $"{sample.DurationMs}");
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
        Assert.Equal((2, 2, 1), (results.Run.SamplesTotal, results.Run.SamplesDone, results.Run.ChecksPassed));
        Assert.Equal(1, results.Run.ChecksFailed);
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

    private static ToolCalledCheck ReasonFits(string key, string scenarioKey) =>
        new()
        {
            Key = key,
            ScenarioKey = scenarioKey,
            Tool = "refund_order",
            Args = Checks.Items.Json( /*lang=json,strict*/
                """{"order_id":"A-17","reason":{"$judge":{"description":"Says the item arrived damaged or broken"}}}"""
            ),
        };

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The recorded model holds only memory.")]
    public async Task A_check_with_a_judge_matcher_gets_its_verdict_from_the_argument_judge_not_from_the_judge_check_model()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [ReasonFits("reason-fits", "refund"), SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var arguments = RecordedModel.Answering("argument-incorrect.json");

        var (run, executor) = await ClaimAsync(agent, argumentJudge: arguments.ArgumentJudge());
        await executor.ExecuteAsync(run, Cancellation);
        var results = await ResultsAsync(run.Id);

        var checks = Assert.Single(results.Scenarios).Checks.ToDictionary(check => check.Key);
        var sample = Assert.Single(checks["reason-fits"].Samples);
        Assert.False(sample.Passed);
        Assert.False(sample.NotJudged);
        Assert.Contains("args.reason does not fit", sample.Message, StringComparison.Ordinal);
        Assert.Equal("refund_order", Assert.IsType<ToolCall>(Assert.Single(sample.Evidence)).Name);
        Assert.True(checks["refund-called"].Passed);
        Assert.Single(arguments.Requests);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The recorded model holds only memory.")]
    public async Task A_judge_matcher_the_model_cannot_judge_is_not_judged_and_the_run_goes_on()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [ReasonFits("reason-fits", "refund"), SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var failing = RecordedModel.Failing(new HttpRequestException("connection refused")).ArgumentJudge();

        var (run, executor) = await ClaimAsync(agent, argumentJudge: failing);
        await executor.ExecuteAsync(run, Cancellation);
        var results = await ResultsAsync(run.Id);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        var checks = Assert.Single(results.Scenarios).Checks.ToDictionary(check => check.Key);
        var sample = Assert.Single(checks["reason-fits"].Samples);
        Assert.True(sample.NotJudged);
        Assert.False(sample.Passed);
        Assert.Contains("connection refused", sample.Message, StringComparison.Ordinal);
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
                redeployed?.ServeContract(SeededAgent.ExampleVersionConfigured("Redeployed mid-run."));
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

    [Fact]
    public async Task A_scenario_is_called_as_often_as_its_most_demanding_check_and_each_check_is_judged_on_its_first_samples_only()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: 3)],
            [
                SeededAgent.RefundAfterLookup("once", "refund") with
                {
                    Samples = 1,
                },
                SeededAgent.RefundAfterLookup("twice", "refund") with
                {
                    Samples = 2,
                },
                SeededAgent.RefundedEvent("as-scenario", "refund"),
            ]
        );

        var results = await RunAsync(agent, parallel: 1);

        Assert.Equal(3, fake.Requests.Count);
        Assert.Equal((3, 3), (results.Run.SamplesTotal, results.Run.SamplesDone));
        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal([1, 2, 3], scenario.Samples.Select(sample => sample.Sample));
        var checks = scenario.Checks.ToDictionary(check => check.Key);
        Assert.Equal([1], checks["once"].Samples.Select(sample => sample.Sample));
        Assert.Equal([1, 2], checks["twice"].Samples.Select(sample => sample.Sample));
        Assert.Equal([1, 2, 3], checks["as-scenario"].Samples.Select(sample => sample.Sample));
        Assert.All(checks.Values, check => Assert.True(check.Passed));
        Assert.Equal((3, 0), (results.Run.ChecksPassed, results.Run.ChecksFailed));
    }

    [Fact]
    public async Task A_check_that_asks_for_more_samples_than_its_scenario_raises_the_calls()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: 1)],
            [SeededAgent.RefundAfterLookup("many", "refund") with { Samples = 4 }]
        );

        var results = await RunAsync(agent, parallel: 1);

        Assert.Equal(4, fake.Requests.Count);
        Assert.Equal(4, Assert.Single(Assert.Single(results.Scenarios).Checks).Samples.Count);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(2, 2)]
    public async Task A_scenario_without_samples_takes_the_agents_and_then_three(int? agentSamples, int expected)
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: null)],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")],
            agentSamples: agentSamples
        );

        var results = await RunAsync(agent, parallel: 1);

        Assert.Equal(expected, fake.Requests.Count);
        Assert.Equal(expected, Assert.Single(Assert.Single(results.Scenarios).Checks).Samples.Count);
    }

    [Fact]
    public async Task A_scenario_without_checks_is_called_its_own_number_of_times()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: null)],
            [],
            agentSamples: 4
        );

        await RunAsync(agent, parallel: 1);

        Assert.Equal(4, fake.Requests.Count);
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
    public async Task More_than_one_turn_without_a_simulated_user_is_recorded_as_failed_without_calling_the_agent()
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
        Assert.Contains("needs a simulated user", scenario.Samples[0].Error, StringComparison.Ordinal);
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

        var tagged = await RunAsync(agent, ["regression"]);
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

    private const string Angry = "Anna wants her money back for a broken kettle and is annoyed.";

    private async Task<RunResults> SimulateAsync(SeededAgent agent, UserSimulator simulator)
    {
        var (run, executor) = await ClaimAsync(agent, simulator: simulator);
        await executor.ExecuteAsync(run, Cancellation);
        return await ResultsAsync(run.Id);
    }

    private static IReadOnlyDictionary<string, string> Baggage(IReadOnlyDictionary<string, string> headers) =>
        SuwagevalBaggage.Decode([headers["baggage"]]);

    private static string? ConversationIdOf(string body) =>
        JsonDocument.Parse(body).RootElement.TryGetProperty("conversationId", out var id) ? id.GetString() : null;

    [Fact]
    public async Task A_simulation_plays_every_turn_and_each_turn_but_the_first_opens_with_the_simulated_user()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn, TimeSpan.FromMilliseconds(30));
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("angry", turns: 3, simulatedUser: Angry)],
            [
                new TextCheck
                {
                    Key = "greets",
                    ScenarioKey = "angry",
                    Contains = "how can I help",
                },
            ]
        );

        var results = await SimulateAsync(agent, RecordedModel.SimulatorAnswering("simulated-message.json"));

        var scenario = Assert.Single(results.Scenarios);
        var sample = Assert.Single(scenario.Samples);
        Assert.Equal(SampleStatus.Done, sample.Status);
        Assert.False(sample.EndedByUser);
        Assert.Equal(
            ["AssistantMessage", "UserMessage", "AssistantMessage", "UserMessage", "AssistantMessage"],
            sample.Output?.Select(item => item.GetType().Name)
        );
        Assert.Equal("It arrived broken, I want my money back.", Assert.IsType<UserMessage>(sample.Output?[1]).Text);
        Assert.Equal(3, sample.TurnDurationsMs?.Count);
        Assert.Equal(sample.TurnDurationsMs?.Sum(), sample.DurationMs);
        Assert.True(Assert.Single(scenario.Checks).Passed);

        var bodies = fake.Requests.ToList();
        Assert.Equal(3, bodies.Count);
        var id = ConversationIdOf(bodies[0]);
        Assert.False(string.IsNullOrEmpty(id));
        Assert.All(bodies, body => Assert.Equal(id, ConversationIdOf(body)));
        Assert.Equal(5, JsonDocument.Parse(bodies[2]).RootElement.GetProperty("conversation").GetArrayLength());
    }

    [Fact]
    public async Task The_simulated_user_may_end_the_conversation_before_the_last_turn()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("leaves", turns: 5, simulatedUser: Angry)],
            []
        );

        var results = await SimulateAsync(agent, RecordedModel.SimulatorAnswering("simulated-message.json", "simulated-end.json"));

        var sample = Assert.Single(Assert.Single(results.Scenarios).Samples);
        Assert.Equal(SampleStatus.Done, sample.Status);
        Assert.True(sample.EndedByUser);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal(2, sample.TurnDurationsMs?.Count);
    }

    [Fact]
    public async Task A_conversation_that_ends_with_the_agent_lets_the_simulated_user_open_the_first_turn()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [
                SeededAgent.Scenario(
                    "greeted",
                    turns: 1,
                    simulatedUser: Angry,
                    conversation: [new AssistantMessage { Text = "Hello! How can I help?" }]
                ),
            ],
            []
        );

        var results = await SimulateAsync(agent, RecordedModel.SimulatorAnswering("simulated-message.json"));

        var sample = Assert.Single(Assert.Single(results.Scenarios).Samples);
        Assert.Equal(["UserMessage", "AssistantMessage"], sample.Output?.Select(item => item.GetType().Name));
        Assert.Equal(2, JsonDocument.Parse(Assert.Single(fake.Requests)).RootElement.GetProperty("conversation").GetArrayLength());
    }

    [Fact]
    public async Task Checks_of_a_simulation_look_at_every_turn()
    {
        await using var fake = await FakeAgent.StartAsync(
            (_, call) => Results.Text(call == 2 ? FakeAgent.RefundTurn : FakeAgent.PlainTurn, "application/json")
        );
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("later", turns: 3, simulatedUser: Angry)],
            [
                SeededAgent.RefundAfterLookup("refunded-at-some-point", "later"),
                new ToolNotCalledCheck
                {
                    Key = "never-refunds-in-the-last-turn",
                    ScenarioKey = "later",
                    Tool = "refund_order",
                    Where = new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Last) },
                },
            ]
        );

        var results = await SimulateAsync(agent, RecordedModel.SimulatorAnswering("simulated-message.json"));

        var scenario = Assert.Single(results.Scenarios);
        Assert.All(scenario.Checks, check => Assert.True(check.Passed, check.Key));
    }

    [Fact]
    public async Task A_simulated_user_who_cannot_be_played_stops_the_sample_as_not_simulated_and_keeps_what_happened()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("broken-model", turns: 3, simulatedUser: Angry)],
            [
                new TextCheck
                {
                    Key = "greets-anyway",
                    ScenarioKey = "broken-model",
                    Contains = "Hello",
                },
            ]
        );

        var results = await SimulateAsync(agent, RecordedModel.SimulatorAnswering("garbage.txt"));

        var scenario = Assert.Single(results.Scenarios);
        var sample = Assert.Single(scenario.Samples);
        Assert.Equal(SampleStatus.NotSimulated, sample.Status);
        Assert.StartsWith("Not simulated: the simulator model's answer is not the JSON asked for", sample.Error, StringComparison.Ordinal);
        Assert.IsType<AssistantMessage>(Assert.Single(sample.Output ?? []));
        var check = Assert.Single(scenario.Checks);
        Assert.False(check.Passed);
        Assert.StartsWith("Not evaluated: Not simulated", check.Samples[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_agent_that_fails_in_a_later_turn_fails_the_sample_and_keeps_the_turns_before()
    {
        await using var fake = await FakeAgent.StartAsync(
            (_, call) => call == 1 ? Results.Text(FakeAgent.PlainTurn, "application/json") : Results.StatusCode(500)
        );
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("down-later", turns: 3, simulatedUser: Angry)],
            []
        );

        var results = await SimulateAsync(agent, RecordedModel.SimulatorAnswering("simulated-message.json"));

        var sample = Assert.Single(Assert.Single(results.Scenarios).Samples);
        Assert.Equal(SampleStatus.Failed, sample.Status);
        Assert.StartsWith("Turn 2: The agent answered 500", sample.Error, StringComparison.Ordinal);
        Assert.Equal(["AssistantMessage", "UserMessage"], sample.Output?.Select(item => item.GetType().Name));
        Assert.Equal(2, sample.TurnDurationsMs?.Count);
    }

    [Fact]
    public async Task A_scenario_of_one_turn_sends_a_conversation_id_of_its_own()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeededAgent.CreateAsync(database.Pool, fake.Url.AbsoluteUri, [SeededAgent.Scenario("once")], []);

        var results = await RunAsync(agent);

        Assert.True(Guid.TryParse(ConversationIdOf(Assert.Single(fake.Requests)), out _));
        var sample = Assert.Single(Assert.Single(results.Scenarios).Samples);
        Assert.Null(sample.TurnDurationsMs);
        Assert.False(sample.EndedByUser);
    }
}
