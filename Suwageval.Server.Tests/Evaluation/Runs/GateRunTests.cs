using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Invocation;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Tests.Judges;

namespace Suwageval.Server.Tests.Evaluation.Runs;

/// <summary>A gate run (<c>suwageval gate</c>) end to end in the worker, against a real PostgreSQL and a fake agent over real HTTP.</summary>
public sealed class GateRunTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static ScenarioInput Guarded(string key, string id, int? retryLimit = null, int samples = 3) =>
        SeededAgent.Scenario(key, samples: samples, conversation: [new UserMessage { Text = $"Please refund order A-17 ({key})." }]) with
        {
            AcGuardrail = new AcGuardrail { Id = id, RetryLimit = retryLimit },
        };

    /// <summary>An agent that refunds (passes the refund check) only on the given calls, else answers plainly.</summary>
    private static Task<FakeAgent> RefundingOnAsync(params int[] calls) =>
        FakeAgent.StartAsync(
            (_, call) => Results.Text(calls.Contains(call) ? FakeAgent.RefundTurn : FakeAgent.PlainTurn, "application/json")
        );

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The client lives as long as the test process."
    )]
    private async Task<RunResults> GateAsync(SeededAgent agent, int? agentRetryLimit = 5, int parallel = 2)
    {
        var store = new RunStore(database.Pool);
        await store.EnqueueAsync(
            agent.Id,
            agent.VersionId,
            [],
            agent.EndpointUrl,
            gateRetryLimit: agentRetryLimit,
            cancellationToken: Cancellation
        );
        var run = await store.ClaimNextAsync(agent.Id, Cancellation) ?? throw new InvalidOperationException("nothing was queued");
        var executor = new RunExecutor(
            store,
            new ScenarioStore(database.Pool),
            new CheckStore(database.Pool),
            new AgentStore(database.Pool),
            new AgentClient(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }),
            RecordedModel.SimulatorAnswering("simulated-end.json"),
            RecordedModel.JudgeAnswering("passed.json"),
            RecordedModel.Answering("passed.json").ArgumentJudge(),
            Options.Create(new RunOptions { MaxParallelScenarios = parallel }),
            NullLogger<RunExecutor>.Instance
        );
        await executor.ExecuteAsync(run, Cancellation);
        var done = await store.GetAsync(run.Id, Cancellation) ?? throw new InvalidOperationException("no such run");
        return RunReport.Results(
            done,
            await store.ListScenarioResultsAsync(run.Id, Cancellation),
            await store.ListCheckResultsAsync(run.Id, Cancellation)
        );
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-4")]
    [Trait("criterion", "AC-gate-runs-5")]
    public async Task A_gate_runs_only_the_guarded_scenarios_each_until_its_first_attempt_that_passes_every_check()
    {
        await using var fake = await RefundingOnAsync(2);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [Guarded("refund", "AC-refunds-1"), SeededAgent.Scenario("not-guarded", samples: 1)],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundAfterLookup("other-called", "not-guarded")]
        );

        var results = await GateAsync(agent);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal("refund", scenario.Key);
        Assert.Equal(
            new ScenarioGuardrailResult
            {
                Id = "AC-refunds-1",
                RetryLimit = 5,
                Attempts = 2,
                Passed = true,
            },
            scenario.AcGuardrail
        );
        Assert.Equal(new GateResult { Passed = true }, results.Gate);
        Assert.Equal(2, fake.Requests.Count);
        Assert.DoesNotContain(fake.Requests, request => request.Contains("not-guarded", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-6")]
    [Trait("criterion", "AC-gate-runs-7")]
    public async Task A_criterion_that_never_passes_gets_exactly_its_limit_of_attempts_and_fails_the_gate()
    {
        await using var fake = await RefundingOnAsync();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [Guarded("own-limit", "AC-refunds-2", retryLimit: 2)],
            [SeededAgent.RefundAfterLookup("refund-called", "own-limit")]
        );

        var results = await GateAsync(agent);

        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal(
            new ScenarioGuardrailResult
            {
                Id = "AC-refunds-2",
                RetryLimit = 2,
                Attempts = 2,
                Passed = false,
            },
            scenario.AcGuardrail
        );
        Assert.Equal(new GateResult { Passed = false, FailedGuardrail = "AC-refunds-2" }, results.Gate);
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-6")]
    public async Task Without_a_limit_of_its_own_a_scenario_gets_the_agents_limit()
    {
        await using var fake = await RefundingOnAsync();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [Guarded("agent-limit", "AC-refunds-3")],
            [SeededAgent.RefundAfterLookup("refund-called", "agent-limit")]
        );

        var results = await GateAsync(agent, agentRetryLimit: 3);

        Assert.Equal(3, Assert.Single(results.Scenarios).AcGuardrail?.Attempts);
        Assert.Equal(3, fake.Requests.Count);
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-7")]
    public async Task The_first_failed_criterion_stops_the_gate_and_the_scenarios_not_started_are_not_run()
    {
        await using var fake = await RefundingOnAsync();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [Guarded("a-fails", "AC-refunds-1", retryLimit: 1), Guarded("b-never-runs", "AC-refunds-2")],
            [SeededAgent.RefundAfterLookup("a-called", "a-fails"), SeededAgent.RefundAfterLookup("b-called", "b-never-runs")]
        );

        var results = await GateAsync(agent, parallel: 1);

        Assert.Equal(RunStatus.Done, results.Run.Status);
        Assert.Equal("a-fails", Assert.Single(results.Scenarios).Key);
        var skipped = Assert.Single(results.Run.SkippedScenarios);
        Assert.Equal(("b-never-runs", "Not run: the gate stopped at AC-refunds-1."), (skipped.Key, skipped.Reason));
        Assert.Equal(new GateResult { Passed = false, FailedGuardrail = "AC-refunds-1" }, results.Gate);
        Assert.Single(fake.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-10")]
    public async Task A_gate_run_is_in_no_list_no_trend_point_and_no_versions_run_count()
    {
        await using var fake = await RefundingOnAsync(1);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [Guarded("refund", "AC-refunds-1")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        await GateAsync(agent);
        var store = new RunStore(database.Pool);

        foreach (var grouping in Enum.GetValues<RunGrouping>())
        {
            Assert.Empty(await store.ListPageAsync(agent.Id, [], 0, 10, grouping: grouping, cancellationToken: Cancellation));
            Assert.Equal(0, await store.CountAsync(agent.Id, [], grouping: grouping, cancellationToken: Cancellation));
        }
        Assert.Empty(EffectiveRuns.Pick(await store.ListAsync(agent.Id, Cancellation), 10));
        Assert.Empty(await new Server.Agents.Versions.VersionStore(database.Pool).RunCountsAsync(agent.Id, Cancellation));
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-11")]
    public async Task An_ordinary_run_names_the_criterion_a_scenario_guards_without_attempts()
    {
        await using var fake = await RefundingOnAsync(1, 2, 3);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [Guarded("refund", "AC-refunds-1"), SeededAgent.Scenario("plain", samples: 1)],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var results = await GateAsync(agent, agentRetryLimit: null);

        Assert.Null(results.Gate);
        Assert.Equal(
            new ScenarioGuardrailResult { Id = "AC-refunds-1" },
            results.Scenarios.Single(scenario => scenario.Key == "refund").AcGuardrail
        );
        Assert.Null(results.Scenarios.Single(scenario => scenario.Key == "plain").AcGuardrail);
    }
}
