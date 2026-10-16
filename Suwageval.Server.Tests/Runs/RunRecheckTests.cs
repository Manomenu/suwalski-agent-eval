using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Suwageval.Contracts;
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
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Runs;

/// <summary>A re-check end to end: a finished run, a changed check, the stored answers judged again — against a real PostgreSQL, no model.</summary>
public sealed class RunRecheckTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RunStore Store() => new(database.Pool);

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    private static ToolCalledCheck Changed(string key, string scenarioKey) =>
        SeededAgent.RefundAfterLookup(key, scenarioKey) with
        {
            Args = Json( /*lang=json,strict*/
                """{"order_id":"B-99"}"""
            ),
        };

    private static JudgeCheck Polite(string key, string scenarioKey, string criteria = "The assistant is polite.") =>
        new()
        {
            Key = key,
            ScenarioKey = scenarioKey,
            Criteria = criteria,
        };

    private async Task UpsertCheckAsync(SeededAgent agent, CheckInput check) =>
        await new CheckStore(database.Pool).UpsertAsync(
            agent.Id,
            [new CheckItem(check.Key, check.ScenarioKey, JsonSerializer.Serialize(check, ContractJson.Options))],
            Cancellation
        );

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The client lives as long as the test process."
    )]
    private async Task<RunRow> ExecuteNextAsync(SeededAgent agent, Judge? judge = null)
    {
        var store = Store();
        var run = await store.ClaimNextAsync(agent.Id, Cancellation) ?? throw new InvalidOperationException("nothing is queued");
        var scenarios = new ScenarioStore(database.Pool);
        var checks = new CheckStore(database.Pool);
        var options = Options.Create(new RunOptions { MaxParallelScenarios = 2 });
        var answering = judge ?? RecordedModel.JudgeAnswering("passed.json");
        var argumentJudge = RecordedModel.Answering("passed.json").ArgumentJudge();
        if (run.RecheckOf is null)
        {
            await new RunExecutor(
                store,
                scenarios,
                checks,
                new AgentStore(database.Pool),
                new AgentClient(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }),
                answering,
                argumentJudge,
                options,
                NullLogger<RunExecutor>.Instance
            ).ExecuteAsync(run, Cancellation);
        }
        else
        {
            await new RunRechecker(
                store,
                scenarios,
                checks,
                answering,
                argumentJudge,
                options,
                NullLogger<RunRechecker>.Instance
            ).ExecuteAsync(run, Cancellation);
        }
        return await store.GetAsync(run.Id, Cancellation) ?? throw new InvalidOperationException("no such run");
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

    private async Task<Guid> FinishedRunAsync(SeededAgent agent, Judge? judge = null)
    {
        var id = await Store().EnqueueAsync(agent.Id, agent.VersionId, null, agent.EndpointUrl, cancellationToken: Cancellation);
        var run = await ExecuteNextAsync(agent, judge);
        Assert.Equal((id, RunStatus.Done), (run.Id, run.State));
        return id;
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, Guid runId) =>
        client.PostAsync(new Uri($"/runs/{runId}/recheck", UriKind.Relative), null, Cancellation);

    private static async Task<RecheckPlan> PlanAsync(HttpClient client, Guid runId) =>
        await client.GetFromJsonAsync<RecheckPlan>(new Uri($"/runs/{runId}/recheck", UriKind.Relative), ContractJson.Options, Cancellation)
        ?? throw new InvalidOperationException("no body");

    private static async Task<Guid> RecheckAsync(HttpClient client, Guid runId)
    {
        var response = await Post(client, runId);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RunCreated>(Cancellation))!.RunId;
    }

    [Fact]
    public async Task A_recheck_copies_the_answers_judges_a_changed_check_again_and_copies_the_verdict_of_an_unchanged_one()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund", samples: 2)],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );
        var source = await FinishedRunAsync(agent);
        var turnsBefore = fake.Requests.Count;
        var contractReadsBefore = fake.ContractRequests;
        await UpsertCheckAsync(agent, Changed("refund-called", "refund"));

        var client = Client();
        var recheckId = await RecheckAsync(client, source);
        var recheck = await ExecuteNextAsync(agent);

        Assert.Equal(recheckId, recheck.Id);
        Assert.Equal((turnsBefore, contractReadsBefore), (fake.Requests.Count, fake.ContractRequests));
        var results = await ResultsAsync(recheckId);
        var original = await ResultsAsync(source);
        Assert.Equal(
            (source, agent.VersionId, false),
            (results.Run.RecheckOf, results.Run.AgentVersionId, results.Run.ContractChangedDuringRun)
        );
        Assert.Equal((2, 2, RunStatus.Done), (results.Run.SamplesTotal, results.Run.SamplesDone, results.Run.Status));
        var scenario = Assert.Single(results.Scenarios);
        Assert.Equal(
            Assert.Single(original.Scenarios).Samples.Select(sample => (sample.Output?.Count, sample.DurationMs)),
            scenario.Samples.Select(sample => (sample.Output?.Count, sample.DurationMs))
        );
        var checks = scenario.Checks.ToDictionary(check => check.Key);
        Assert.False(checks["refund-called"].Passed);
        Assert.Equal(2, checks["refund-called"].Revision);
        Assert.True(checks["refund-event"].Passed);
        Assert.Equal(1, checks["refund-event"].Revision);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The recorded model holds only memory.")]
    public async Task A_judge_verdict_of_an_unchanged_check_is_copied_not_asked_again_and_a_changed_one_is_asked_again()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund"), SeededAgent.Scenario("other")],
            [Polite("polite", "refund"), Polite("polite-other", "other"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );
        var source = await FinishedRunAsync(agent, RecordedModel.JudgeAnswering("failed.json"));
        await UpsertCheckAsync(agent, Polite("polite-other", "other", "The assistant is very polite."));

        await RecheckAsync(Client(), source);
        var model = RecordedModel.Answering("passed.json");
        await ExecuteNextAsync(agent, model.Judge());

        var recheckRun = (await Store().ListAsync(agent.Id, Cancellation)).First(run => run.RecheckOf is not null);
        var scenarios = (await ResultsAsync(recheckRun.Id)).Scenarios.ToDictionary(scenario => scenario.Key);
        var copied = Assert.Single(scenarios["refund"].Checks.Single(check => check.Key == "polite").Samples);
        Assert.False(copied.Passed);
        Assert.Contains("did not refuse", copied.Message, StringComparison.Ordinal);
        Assert.True(scenarios["other"].Checks.Single().Passed);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task A_scenario_that_changed_or_was_deleted_is_skipped_with_its_reason_and_left_out_of_the_results()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("kept"), SeededAgent.Scenario("edited"), SeededAgent.Scenario("gone")],
            [
                SeededAgent.RefundAfterLookup("kept-called", "kept"),
                SeededAgent.RefundAfterLookup("edited-called", "edited"),
                SeededAgent.RefundAfterLookup("gone-called", "gone"),
            ]
        );
        var source = await FinishedRunAsync(agent);
        var edited = SeededAgent.Scenario("edited") with { Conversation = [new UserMessage { Text = "A different question." }] };
        await new ScenarioStore(database.Pool).UpsertAsync(
            agent.Id,
            [new ScenarioItem("edited", edited.Tags, JsonSerializer.Serialize(edited, ContractJson.Options))],
            Cancellation
        );
        await new ScenarioStore(database.Pool).DeleteAsync(agent.Id, "gone", Cancellation);
        await UpsertCheckAsync(agent, Changed("kept-called", "kept"));

        await RecheckAsync(Client(), source);
        var recheck = await ExecuteNextAsync(agent);

        var results = await ResultsAsync(recheck.Id);
        Assert.Equal(["kept"], results.Scenarios.Select(scenario => scenario.Key));
        Assert.Equal(1, results.Run.SamplesTotal);
        Assert.Equal(["edited", "gone"], results.Run.SkippedScenarios.Select(skipped => skipped.Key));
        Assert.Contains("changed", results.Run.SkippedScenarios[0].Reason, StringComparison.Ordinal);
        Assert.Contains("deleted", results.Run.SkippedScenarios[1].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_sample_stays_failed_and_its_checks_are_not_evaluated()
    {
        await using var fake = await FakeAgent.StartAsync((_, _) => Results.Text("boom", statusCode: 500));
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var source = await FinishedRunAsync(agent);
        await UpsertCheckAsync(agent, Changed("refund-called", "refund"));

        await RecheckAsync(Client(), source);
        var recheck = await ExecuteNextAsync(agent);

        var scenario = Assert.Single((await ResultsAsync(recheck.Id)).Scenarios);
        var sample = Assert.Single(scenario.Samples);
        Assert.Equal(SampleStatus.Failed, sample.Status);
        Assert.Contains("500", sample.Error, StringComparison.Ordinal);
        Assert.Contains("Not evaluated", Assert.Single(scenario.Checks).Samples[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_plan_lists_what_changed_and_a_recheck_of_a_recheck_points_at_the_original_run()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );
        var source = await FinishedRunAsync(agent);
        var client = Client();
        await UpsertCheckAsync(agent, Changed("refund-called", "refund"));
        await UpsertCheckAsync(agent, Polite("polite", "refund"));
        await new CheckStore(database.Pool).DeleteAsync(agent.Id, "refund-event", Cancellation);

        var plan = await PlanAsync(client, source);

        Assert.True(plan.Possible);
        Assert.Equal(
            [("polite", CheckChange.Added), ("refund-called", CheckChange.Changed), ("refund-event", CheckChange.Removed)],
            plan.ChangedChecks.Select(change => (change.CheckKey, change.Change))
        );
        Assert.Equal(1, plan.JudgedChecks);
        var changed = plan.ChangedChecks.Single(change => change.Change == CheckChange.Changed);
        Assert.NotNull(changed.Before);
        Assert.NotNull(changed.After);

        await RecheckAsync(client, source);
        var first = await ExecuteNextAsync(agent);
        Assert.Equal(source, first.RecheckOf);
        Assert.False((await PlanAsync(client, first.Id)).Possible);
        await UpsertCheckAsync(agent, Polite("polite", "refund") with { PassThreshold = 0.5 });
        await RecheckAsync(client, first.Id);
        var second = await ExecuteNextAsync(agent);

        Assert.Equal(source, second.RecheckOf);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The recorded model holds only memory.")]
    public async Task A_recheck_of_a_recheck_copies_unchanged_verdicts_from_that_recheck_and_asks_the_judge_nothing()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [Polite("polite", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );
        var source = await FinishedRunAsync(agent, RecordedModel.JudgeAnswering("failed.json"));
        var client = Client();
        await UpsertCheckAsync(agent, Polite("polite", "refund", "The assistant is very polite."));
        var firstId = await RecheckAsync(client, source);
        var firstModel = RecordedModel.Answering("passed.json");
        await ExecuteNextAsync(agent, firstModel.Judge());
        Assert.Single(firstModel.Requests);
        await UpsertCheckAsync(
            agent,
            SeededAgent.RefundedEvent("refund-event", "refund") with
            {
                Data = Json( /*lang=json,strict*/
                    """{"node":"refunded","orderId":"A-17"}"""
                ),
            }
        );

        var secondId = await RecheckAsync(client, firstId);
        var secondModel = RecordedModel.Answering("failed.json");
        var second = await ExecuteNextAsync(agent, secondModel.Judge());

        Assert.Equal((source, firstId), (second.RecheckOf, second.VerdictsFrom));
        Assert.Empty(secondModel.Requests);
        var checks = Assert.Single((await ResultsAsync(secondId)).Scenarios).Checks.ToDictionary(check => check.Key);
        Assert.True(checks["polite"].Passed);
        Assert.Equal(2, checks["polite"].Revision);
        Assert.Equal(2, checks["refund-event"].Revision);
    }

    [Fact]
    public async Task A_recheck_that_would_change_nothing_or_of_an_unfinished_run_is_409_with_the_reason()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var client = Client();
        var source = await FinishedRunAsync(agent);
        var queued = await Store().EnqueueAsync(agent.Id, agent.VersionId, null, agent.EndpointUrl, cancellationToken: Cancellation);

        var unfinished = await PlanAsync(client, queued);
        var unchanged = await PlanAsync(client, source);
        var refused = await Post(client, source);

        Assert.Equal((false, RecheckBlocker.NotFinished), (unfinished.Possible, unfinished.Blocker));
        Assert.Equal((false, RecheckBlocker.NothingChanged), (unchanged.Possible, unchanged.Blocker));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("would give the same results", await refused.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, queued)).StatusCode);
        Assert.DoesNotContain(await Store().ListAsync(agent.Id, Cancellation), run => run.RecheckOf is not null);
    }

    [Fact]
    public async Task A_check_that_does_not_fit_the_version_of_the_run_is_409_with_the_mismatches()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var source = await FinishedRunAsync(agent);
        await UpsertCheckAsync(agent, SeededAgent.RefundAfterLookup("refund-called", "refund") with { Tool = "no_such_tool" });
        var client = Client();

        var plan = await PlanAsync(client, source);
        var refused = await Post(client, source);

        Assert.Equal(RecheckBlocker.Mismatches, plan.Blocker);
        Assert.Contains("no_such_tool", Assert.Single(plan.Mismatches), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("mismatches", await refused.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_whose_every_scenario_changed_cannot_be_re_checked_and_an_unknown_run_is_404()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var source = await FinishedRunAsync(agent);
        await new ScenarioStore(database.Pool).DeleteAsync(agent.Id, "refund", Cancellation);
        var client = Client();

        var plan = await PlanAsync(client, source);

        Assert.Equal(RecheckBlocker.AllSkipped, plan.Blocker);
        Assert.Equal("refund", Assert.Single(plan.SkippedScenarios).Key);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, Guid.NewGuid())).StatusCode);
        var unknown = await client.GetAsync(new Uri($"/runs/{Guid.NewGuid()}/recheck", UriKind.Relative), Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
