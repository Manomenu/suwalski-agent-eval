using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Runs;

/// <summary>The history of a check and the states it leads to, against a real PostgreSQL: runs are written straight into the store.</summary>
public sealed class RegressionHistoryTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RunStore Store() => new(database.Pool);

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The client lives as long as the test process."
    )]
    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        app.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<SeededAgent> NewAgentAsync() =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

    private static RunPlacement Reference(string branch = "master") =>
        new(
            new RunOrigin
            {
                Branch = branch,
                Clean = true,
                Pushed = true,
                DefaultBranch = "master",
            },
            true,
            branch
        );

    private static RunPlacement OnBranch(string branch, string comparedWith = "master") =>
        new(
            new RunOrigin
            {
                Branch = branch,
                Clean = true,
                Pushed = true,
                DefaultBranch = "master",
            },
            false,
            comparedWith
        );

    private async Task<Guid> FinishedRunAsync(
        SeededAgent agent,
        bool[] samplePassed,
        RunPlacement? placement = null,
        Guid? recheckOf = null,
        RunStatus status = RunStatus.Done,
        string? error = null,
        bool[]? errored = null,
        string? runGroup = null
    )
    {
        var store = Store();
        var id = await store.EnqueueAsync(
            agent.Id,
            agent.VersionId,
            [],
            agent.EndpointUrl,
            recheckOf,
            placement: placement,
            runGroup: runGroup,
            cancellationToken: Cancellation
        );
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var check = Assert.Single(await new CheckStore(database.Pool).ListAsync(agent.Id, cancellationToken: Cancellation));
        for (var i = 0; i < samplePassed.Length; i++)
        {
            var failed = error is not null || (errored?[i] ?? false);
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    i + 1,
                    failed ? SampleStatus.Failed : SampleStatus.Done,
                    "[]",
                    failed ? error ?? "HTTP 500" : null,
                    1,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, samplePassed[i], "[]", "m")]
                ),
                Cancellation
            );
        }
        await store.FinishAsync(id, status, cancellationToken: Cancellation);
        return id;
    }

    private Task<Guid> PassAsync(SeededAgent agent, RunPlacement? placement = null) => FinishedRunAsync(agent, [true], placement);

    private Task<Guid> FailAsync(SeededAgent agent, RunPlacement? placement = null) => FinishedRunAsync(agent, [false], placement);

    private async Task<Guid[]> HistoryAsync(Guid run, string? comparedWith, int limit = 10, Guid? anchor = null) =>
        [.. (await Store().ListCheckHistoriesAsync(run, anchor ?? run, comparedWith, limit, Cancellation)).Select(row => row.RunId)];

    [Fact]
    public async Task The_history_is_the_older_reference_runs_of_the_branch_oldest_first()
    {
        var agent = await NewAgentAsync();
        var first = await PassAsync(agent, Reference());
        await PassAsync(agent, Reference("release"));
        await PassAsync(agent, OnBranch("feature/x"));
        await PassAsync(agent);
        await FinishedRunAsync(agent, [true], Reference(), status: RunStatus.Cancelled);
        var second = await FailAsync(agent, Reference());
        var current = await PassAsync(agent, OnBranch("feature/y"));
        await PassAsync(agent, Reference());

        Assert.Equal([first, second], await HistoryAsync(current, "master"));
    }

    [Fact]
    public async Task The_history_keeps_the_newest_runs_only()
    {
        var agent = await NewAgentAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await PassAsync(agent, Reference()));
        }
        var current = await PassAsync(agent, OnBranch("feature/x"));

        Assert.Equal(ids.Skip(2), await HistoryAsync(current, "master", limit: 3));
    }

    [Fact]
    public async Task Without_a_compared_branch_every_older_run_of_the_agent_counts_reference_or_not()
    {
        var agent = await NewAgentAsync();
        var other = await NewAgentAsync();
        var plain = await PassAsync(agent);
        var feature = await FailAsync(agent, OnBranch("feature/x"));
        await PassAsync(other);
        var current = await PassAsync(agent);

        Assert.Equal([plain, feature], await HistoryAsync(current, null));
    }

    [Fact]
    public async Task A_changed_check_starts_its_history_again()
    {
        var agent = await NewAgentAsync();
        await PassAsync(agent, Reference());
        await PassAsync(agent, Reference());
        await new CheckStore(database.Pool).UpsertAsync(
            agent.Id,
            [
                new CheckItem(
                    "refund-called",
                    "refund",
                    System.Text.Json.JsonSerializer.Serialize(
                        SeededAgent.RefundAfterLookup("refund-called", "refund", passThreshold: 0.5),
                        ContractJson.Options
                    )
                ),
            ],
            Cancellation
        );
        var onNewRevision = await PassAsync(agent, Reference());
        var current = await PassAsync(agent, OnBranch("feature/x"));

        Assert.Equal([onNewRevision], await HistoryAsync(current, "master"));
    }

    [Fact]
    public async Task A_re_checked_run_counts_with_the_verdicts_of_its_newest_finished_re_check_and_a_re_check_is_never_a_point()
    {
        var agent = await NewAgentAsync();
        var source = await FailAsync(agent, Reference());
        await FinishedRunAsync(agent, [true], Reference(), recheckOf: source);
        await FinishedRunAsync(agent, [false], Reference(), recheckOf: source, status: RunStatus.Cancelled);
        var current = await PassAsync(agent, OnBranch("feature/x"));

        var rows = await Store().ListCheckHistoriesAsync(current, current, "master", 10, Cancellation);

        var row = Assert.Single(rows);
        Assert.Equal((source, 1, 1), (row.RunId, row.Samples, row.SamplesPassed));
    }

    [Fact]
    public async Task A_re_check_stands_where_the_run_it_was_made_from_stood()
    {
        var agent = await NewAgentAsync();
        var first = await PassAsync(agent, Reference());
        var source = await PassAsync(agent, Reference());
        await PassAsync(agent, Reference());
        var recheck = await FinishedRunAsync(agent, [false], Reference(), recheckOf: source);

        Assert.Equal([first], await HistoryAsync(recheck, "master", anchor: source));
    }

    [Fact]
    public async Task A_check_with_samples_counts_as_passed_when_its_pass_rate_reaches_the_threshold()
    {
        var agent = await NewAgentAsync();
        await FinishedRunAsync(agent, [true, false], Reference());
        var current = await PassAsync(agent, OnBranch("feature/x"));

        var row = Assert.Single(await Store().ListCheckHistoriesAsync(current, current, "master", 10, Cancellation));

        Assert.Equal((2, 1, 1.0), (row.Samples, row.SamplesPassed, row.PassThreshold));
    }

    private async Task<RunResults> ResultsAsync(Guid run) =>
        await Client().GetFromJsonAsync<RunResults>(new Uri($"/runs/{run}/results", UriKind.Relative), ContractJson.Options, Cancellation)
        ?? throw new InvalidOperationException("no body");

    [Fact]
    public async Task A_failing_run_on_a_branch_after_five_passing_reference_runs_is_a_regression()
    {
        var agent = await NewAgentAsync();
        List<Guid> passed = [];
        for (var i = 0; i < 5; i++)
        {
            passed.Add(await PassAsync(agent, Reference()));
        }
        var last = passed[^1];
        var current = await FailAsync(agent, OnBranch("feature/x"));

        var results = await ResultsAsync(current);

        var check = Assert.Single(Assert.Single(results.Scenarios).Checks);
        Assert.Equal(
            (RegressionState.Regression, last, null),
            (check.Regression!.State, check.Regression.LastPassedRun, check.Regression.FailingSince)
        );
        Assert.Equal(passed, check.Regression.History.Select(point => point.RunId));
        Assert.All(check.Regression.History, point => Assert.True(point.Passed));
        Assert.Equal(check.Regression.History.OrderBy(point => point.CreatedAt), check.Regression.History);
        var summary = results.Regression!;
        Assert.Equal(("master", 5, 1, 0), (summary.ComparedWith, summary.History, summary.Regressions, summary.New));
    }

    [Fact]
    public async Task Failing_runs_after_passing_ones_are_already_broken_since_the_first_failure()
    {
        var agent = await NewAgentAsync();
        Guid lastPassed = default;
        for (var i = 0; i < 5; i++)
        {
            lastPassed = await PassAsync(agent, Reference());
        }
        var firstFailure = await FailAsync(agent, Reference());
        var current = await FailAsync(agent, Reference());

        var results = await ResultsAsync(current);

        var regression = Assert.Single(Assert.Single(results.Scenarios).Checks).Regression!;
        Assert.Equal(
            (RegressionState.AlreadyBroken, firstFailure, lastPassed),
            (regression.State, regression.FailingSince, regression.LastPassedRun)
        );
        Assert.Equal(1, results.Regression!.AlreadyBroken);
    }

    [Fact]
    public async Task A_check_with_less_history_than_the_agent_asks_for_is_new_when_it_fails_and_the_agents_history_setting_is_read_at_once()
    {
        var agent = await NewAgentAsync();
        await PassAsync(agent, Reference());
        await PassAsync(agent, Reference());
        var current = await FailAsync(agent, OnBranch("feature/x"));
        var before = (await ResultsAsync(current)).Regression!;
        await new AgentStore(database.Pool).SetSettingsAsync(agent.Id, null, new RegressionSettings { History = 2 }, Cancellation);

        var after = await ResultsAsync(current);

        Assert.Equal((5, 1, 0), (before.History, before.New, before.Regressions));
        Assert.Equal((2, 0, 1), (after.Regression!.History, after.Regression.New, after.Regression.Regressions));
    }

    [Fact]
    public async Task A_run_that_is_not_done_has_no_regression_and_a_check_that_fails_only_for_lack_of_an_answer_is_an_agent_error()
    {
        var agent = await NewAgentAsync();
        var running = await FinishedRunAsync(agent, [false], Reference(), status: RunStatus.Failed);
        var failedAgent = await FinishedRunAsync(agent, [false], Reference(), error: "the agent did not answer");

        var notDone = await ResultsAsync(running);
        var done = await ResultsAsync(failedAgent);

        Assert.Null(notDone.Regression);
        Assert.All(notDone.Scenarios.SelectMany(scenario => scenario.Checks), check => Assert.Null(check.Regression));
        Assert.Equal(RegressionState.AgentError, Assert.Single(Assert.Single(done.Scenarios).Checks).Regression!.State);
        Assert.Equal(1, done.Regression!.AgentErrors);
    }

    [Fact]
    public async Task A_run_where_every_sample_had_an_agent_error_is_not_a_point_of_the_history()
    {
        var agent = await NewAgentAsync();
        var passing = await PassAsync(agent, Reference());
        await FinishedRunAsync(agent, [false], Reference(), error: "HTTP 500");
        var current = await PassAsync(agent, OnBranch("feature/x"));

        var rows = await Store().ListCheckHistoriesAsync(current, current, "master", 10, Cancellation);

        Assert.Equal([passing], rows.Select(row => row.RunId));
    }

    [Fact]
    public async Task A_run_with_some_agent_errors_is_counted_over_its_evaluated_samples_only()
    {
        var agent = await NewAgentAsync();
        var partly = await FinishedRunAsync(agent, [true, false, false], Reference(), errored: [false, true, true]);
        var current = await PassAsync(agent, OnBranch("feature/x"));

        var row = Assert.Single(await Store().ListCheckHistoriesAsync(current, current, "master", 10, Cancellation));

        Assert.Equal((partly, 1, 1), (row.RunId, row.Samples, row.SamplesPassed));
    }

    [Fact]
    public async Task A_failing_check_whose_evaluated_samples_reach_the_threshold_is_an_agent_error_and_one_whose_do_not_is_a_regression()
    {
        var agent = await NewAgentAsync();
        for (var i = 0; i < 5; i++)
        {
            await PassAsync(agent, Reference());
        }
        var onlyErrors = await FinishedRunAsync(agent, [true, false], OnBranch("feature/x"), errored: [false, true]);
        var realFailure = await FinishedRunAsync(agent, [false, false], OnBranch("feature/y"), errored: [false, true]);

        var errorState = Assert.Single(Assert.Single((await ResultsAsync(onlyErrors)).Scenarios).Checks).Regression!;
        var failureState = Assert.Single(Assert.Single((await ResultsAsync(realFailure)).Scenarios).Checks).Regression!;

        Assert.Equal(RegressionState.AgentError, errorState.State);
        Assert.Equal(5, errorState.History.Count);
        Assert.Equal(RegressionState.Regression, failureState.State);
    }

    private static RunOrigin MasterOrigin() =>
        new()
        {
            Branch = "master",
            Clean = true,
            Pushed = true,
            DefaultBranch = "master",
        };

    [Fact]
    public async Task A_new_run_stores_its_origin_and_whether_it_is_a_reference_and_a_rerun_inherits_them()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var client = Client();

        var response = await client.PostAsJsonAsync(
            new Uri("/runs", UriKind.Relative),
            new RunRequest { Agent = agent.Name, Origin = MasterOrigin() },
            Cancellation
        );
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<RunCreated>(Cancellation))!;
        // The agent's settings changed since: the rerun keeps what the run it repeats decided.
        await new AgentStore(database.Pool).SetSettingsAsync(agent.Id, null, new RegressionSettings { Branches = ["other"] }, Cancellation);
        var rerun = await client.PostAsync(new Uri($"/runs/{created.RunId}/rerun", UriKind.Relative), null, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, rerun.StatusCode);
        var rerunId = (await rerun.Content.ReadFromJsonAsync<RunCreated>(Cancellation))!.RunId;

        var original = (
            await client.GetFromJsonAsync<RunSummary>(
                new Uri($"/runs/{created.RunId}", UriKind.Relative),
                ContractJson.Options,
                Cancellation
            )
        )!;
        var repeated = (
            await client.GetFromJsonAsync<RunSummary>(new Uri($"/runs/{rerunId}", UriKind.Relative), ContractJson.Options, Cancellation)
        )!;

        Assert.Equal((true, "master", "master"), (original.Reference, original.ComparedWith, original.Origin?.Branch));
        Assert.Equal((true, "master", "master"), (repeated.Reference, repeated.ComparedWith, repeated.Origin?.Branch));
    }

    private static async Task<RunCreated> StartAsync(HttpClient client, RunRequest request) =>
        (
            await (
                await client.PostAsJsonAsync(new Uri("/runs", UriKind.Relative), request, Cancellation)
            ).Content.ReadFromJsonAsync<RunCreated>(Cancellation)
        )!;

    [Fact]
    public async Task A_run_in_a_group_is_an_experiment_never_a_reference_whatever_its_origin_or_the_run_it_repeats()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var client = Client();
        var grouped = await StartAsync(
            client,
            new RunRequest
            {
                Agent = agent.Name,
                Origin = MasterOrigin(),
                Group = "sweep-1",
            }
        );
        var reference = await StartAsync(client, new RunRequest { Agent = agent.Name, Origin = MasterOrigin() });
        var rerun = await (
            await client.PostAsJsonAsync(
                new Uri($"/runs/{reference.RunId}/rerun", UriKind.Relative),
                new RerunRequest { Group = "sweep-1" },
                Cancellation
            )
        ).Content.ReadFromJsonAsync<RunCreated>(Cancellation);

        foreach (var id in new[] { grouped.RunId, rerun!.RunId })
        {
            var run = (
                await client.GetFromJsonAsync<RunSummary>(new Uri($"/runs/{id}", UriKind.Relative), ContractJson.Options, Cancellation)
            )!;
            Assert.Equal((false, null), (run.Reference, run.ComparedWith));
        }
        Assert.True(
            (
                await client.GetFromJsonAsync<RunSummary>(
                    new Uri($"/runs/{reference.RunId}", UriKind.Relative),
                    ContractJson.Options,
                    Cancellation
                )
            )!.Reference
        );
    }

    [Fact]
    public async Task A_run_in_a_group_and_a_re_check_of_it_get_no_regression_states_and_are_no_history_of_others()
    {
        var agent = await NewAgentAsync();
        var experiment = await FinishedRunAsync(agent, [false], runGroup: "sweep-1");
        var recheck = await FinishedRunAsync(agent, [false], recheckOf: experiment);
        var plain = await FailAsync(agent);

        var inGroup = await ResultsAsync(experiment);
        var recheckOfGroup = await ResultsAsync(recheck);

        Assert.All([inGroup, recheckOfGroup], results => Assert.Null(results.Regression));
        Assert.All(
            [inGroup, recheckOfGroup],
            results => Assert.All(results.Scenarios.SelectMany(scenario => scenario.Checks), check => Assert.Null(check.Regression))
        );
        Assert.Empty(await HistoryAsync(plain, null));
    }

    [Fact]
    public async Task A_run_without_an_origin_is_no_reference()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var client = Client();

        var created = await (
            await client.PostAsJsonAsync(new Uri("/runs", UriKind.Relative), new RunRequest { Agent = agent.Name }, Cancellation)
        ).Content.ReadFromJsonAsync<RunCreated>(Cancellation);
        var run = (
            await client.GetFromJsonAsync<RunSummary>(
                new Uri($"/runs/{created!.RunId}", UriKind.Relative),
                ContractJson.Options,
                Cancellation
            )
        )!;

        Assert.Equal((false, null, null), (run.Reference, run.ComparedWith, run.Origin));
    }
}
