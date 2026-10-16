using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Cli.Files;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Run;

/// <summary>The command against a stand-in for the server: what it sends, what it prints, which exit code it gives.</summary>
public sealed class RunCommandTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid VersionId = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private sealed class FakeServer(Func<HttpRequestMessage, string?, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri?.AbsolutePath ?? "", body));
            return answer(request, body);
        }
    }

    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, ContractJson.Options), Encoding.UTF8, "application/json"),
        };

    private static RunSummary Summary(RunStatus status, string? error = null) =>
        new()
        {
            Id = RunId,
            Agent = "example-agent",
            AgentVersionId = VersionId,
            Tag = "regression",
            EndpointUrl = "http://localhost:7010",
            Status = status,
            Error = error,
            SamplesTotal = 1,
            SamplesDone = 1,
            ChecksPassed = 0,
            ChecksFailed = 0,
            CreatedAt = DateTime.UtcNow,
        };

    private static CheckRunResult Check(
        string key,
        bool passed,
        string message,
        double threshold = 1.0,
        double rate = -1,
        bool notJudged = false
    ) =>
        new()
        {
            Key = key,
            Revision = 1,
            LatestRevision = 1,
            JudgedByModel = false,
            Definition = new TextCheck
            {
                Key = key,
                ScenarioKey = "s",
                Contains = "x",
            },
            Passed = passed,
            PassRate =
                rate >= 0 ? rate
                : passed ? 1.0
                : 0.0,
            PassThreshold = threshold,
            Samples =
            [
                new CheckSampleResult
                {
                    Sample = 1,
                    Passed = passed,
                    NotJudged = notJudged,
                    Evidence = [],
                    Message = message,
                },
            ],
        };

    private static RunResults Results(RunStatus status, params CheckRunResult[] checks) =>
        new()
        {
            Run = Summary(status),
            Scenarios =
            [
                new ScenarioRunResult
                {
                    Key = "refund-happy-path",
                    Revision = 1,
                    Samples =
                    [
                        new SampleResult
                        {
                            Sample = 1,
                            Status = SampleStatus.Done,
                            Output = [],
                        },
                    ],
                    Checks = checks,
                },
            ],
        };

    private static HttpResponseMessage Server(HttpRequestMessage request, RunSummary state, RunResults results) =>
        (request.Method.Method, request.RequestUri?.AbsolutePath) switch
        {
            ("POST", "/runs") => Json(
                new RunCreated
                {
                    RunId = RunId,
                    AgentVersionId = VersionId,
                    VersionCreated = true,
                },
                HttpStatusCode.Accepted
            ),
            ("GET", var path) when path == $"/runs/{RunId}" => Json(state),
            ("GET", var path) when path == $"/runs/{RunId}/results" => Json(results),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

    /// <summary>A reader with no repository and no CI: the run has no origin (the sample folder sits in this repository, whose state is not the test's business).</summary>
    private static readonly RunOriginReader NoRepository = new((_, _) => null, _ => null);

    private static (int Exit, string Output, string Error, FakeServer Server) Run(
        Func<HttpRequestMessage, string?, HttpResponseMessage> answer,
        params string[] args
    ) => RunWith(NoRepository, answer, args);

    private static (int Exit, string Output, string Error, FakeServer Server) RunWith(
        RunOriginReader origins,
        Func<HttpRequestMessage, string?, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var server = new FakeServer(answer);
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["run", "--dir", Sample, .. args], new StringReader(""), output, error, http, origins: origins);
        return (exit, output.ToString(), error.ToString(), server);
    }

    private static CheckRunResult WithState(
        CheckRunResult check,
        RegressionState state,
        Guid? failingSince = null,
        bool agentFailed = false
    ) =>
        check with
        {
            Regression = new CheckRegression
            {
                State = state,
                FailingSince = failingSince,
                AgentFailed = agentFailed,
            },
        };

    private static RunResults WithSummary(
        RunResults results,
        string? comparedWith,
        int regressions = 0,
        int @new = 0,
        int alreadyBroken = 0
    ) =>
        results with
        {
            Regression = new RegressionSummary
            {
                ComparedWith = comparedWith,
                History = 5,
                Regressions = regressions,
                New = @new,
                AlreadyBroken = alreadyBroken,
                KnownFailures = 0,
                Fixed = 0,
                Unstable = 0,
            },
        };

    private static RunOriginReader Repository(string branch, bool clean = true, bool pushed = true) =>
        new(
            (_, args) =>
                string.Join(' ', args) switch
                {
                    "rev-parse HEAD" => new RunOriginReader.GitAnswer(0, "abc123\n"),
                    "rev-parse --abbrev-ref HEAD" => new RunOriginReader.GitAnswer(0, branch + "\n"),
                    "status --porcelain" => new RunOriginReader.GitAnswer(0, clean ? "" : " M a.yaml\n"),
                    "symbolic-ref --short refs/remotes/origin/HEAD" => new RunOriginReader.GitAnswer(0, "origin/master\n"),
                    _ => new RunOriginReader.GitAnswer(pushed ? 0 : 1, ""),
                },
            _ => null
        );

    [Fact]
    public void Without_a_repository_no_origin_is_sent()
    {
        var (_, _, _, server) = Run((request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued)));

        using var sent = JsonDocument.Parse(server.Requests[0].Body ?? "");
        Assert.False(sent.RootElement.TryGetProperty("origin", out var origin) && origin.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public void The_origin_read_from_the_repository_is_sent()
    {
        var (_, _, _, server) = RunWith(
            Repository("feature-x", pushed: false),
            (request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued))
        );

        using var sent = JsonDocument.Parse(server.Requests[0].Body ?? "");
        var origin = sent.RootElement.GetProperty("origin");
        Assert.Equal("feature-x", origin.GetProperty("branch").GetString());
        Assert.Equal("abc123", origin.GetProperty("commit").GetString());
        Assert.Equal("master", origin.GetProperty("defaultBranch").GetString());
        Assert.True(origin.GetProperty("clean").GetBoolean());
        Assert.False(origin.GetProperty("pushed").GetBoolean());
    }

    [Fact]
    public void Ref_is_sent_as_the_branch_to_compare_with()
    {
        var (_, _, _, server) = RunWith(
            Repository("feature-x"),
            (request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued)),
            "--ref",
            "release"
        );

        using var sent = JsonDocument.Parse(server.Requests[0].Body ?? "");
        Assert.Equal("release", sent.RootElement.GetProperty("origin").GetProperty("compareWith").GetString());
    }

    [Fact]
    public void Ref_without_a_repository_still_names_the_branch()
    {
        var (_, _, _, server) = Run(
            (request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued)),
            "--ref",
            "release"
        );

        using var sent = JsonDocument.Parse(server.Requests[0].Body ?? "");
        var origin = sent.RootElement.GetProperty("origin");
        Assert.Equal("release", origin.GetProperty("compareWith").GetString());
        Assert.False(origin.GetProperty("pushed").GetBoolean());
    }

    [Fact]
    public void A_regression_exits_1_and_is_printed_first()
    {
        var broken = Guid.Parse("8d2e1c3a-0000-0000-0000-000000000000");
        var results = WithSummary(
            Results(
                RunStatus.Done,
                WithState(Check("refund-tool-called", false, "tool 'issue_refund' was not called"), RegressionState.Regression),
                WithState(Check("invoice-attached", false, "no invoice"), RegressionState.AlreadyBroken, broken),
                WithState(Check("polite", true, "ok"), RegressionState.Passed)
            ),
            "master",
            regressions: 1,
            alreadyBroken: 1
        );

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(1, exit);
        var lines = output.ReplaceLineEndings("\n");
        Assert.Contains("compared with master (last 5 reference runs)\n", lines, StringComparison.Ordinal);
        Assert.Contains(
            "\u2717 1 regression\n  refund-happy-path / refund-tool-called  tool 'issue_refund' was not called\n",
            lines,
            StringComparison.Ordinal
        );
        Assert.Contains("! 1 already broken", lines, StringComparison.Ordinal);
        Assert.Contains("failing since run 8d2e1c3a", lines, StringComparison.Ordinal);
        Assert.Contains("3 checks: 1 passed, 2 failed\n", lines, StringComparison.Ordinal);
        Assert.True(lines.IndexOf("regression", StringComparison.Ordinal) < lines.IndexOf("already broken", StringComparison.Ordinal));
    }

    [Fact]
    public void A_new_failing_check_exits_1()
    {
        var results = WithSummary(
            Results(RunStatus.Done, WithState(Check("asks", false, "no question"), RegressionState.New, agentFailed: true)),
            null,
            @new: 1
        );

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(1, exit);
        Assert.Contains("compared with every run of the agent (no git)", output, StringComparison.Ordinal);
        Assert.Contains("1 new check failing", output, StringComparison.Ordinal);
        Assert.Contains("(the agent gave no answer)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Failures_that_are_not_regressions_exit_0_and_known_ones_are_a_count()
    {
        var results = WithSummary(
            Results(
                RunStatus.Done,
                WithState(Check("old", false, "still wrong"), RegressionState.KnownFailure),
                WithState(Check("flaky", false, "sometimes"), RegressionState.Unstable),
                WithState(Check("mended", true, "ok"), RegressionState.Fixed)
            ),
            "master"
        ) with
        {
            Regression = new RegressionSummary
            {
                ComparedWith = "master",
                History = 5,
                Regressions = 0,
                New = 0,
                AlreadyBroken = 0,
                KnownFailures = 1,
                Fixed = 1,
                Unstable = 1,
            },
        };

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("  1 known failure\n".ReplaceLineEndings(), output, StringComparison.Ordinal);
        Assert.DoesNotContain("still wrong", output, StringComparison.Ordinal);
        Assert.Contains("\u2713 1 fixed", output, StringComparison.Ordinal);
        Assert.Contains("refund-happy-path / mended", output, StringComparison.Ordinal);
        Assert.Contains("~ 1 unstable", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Strict_exits_1_on_any_failed_check()
    {
        var results = WithSummary(
            Results(RunStatus.Done, WithState(Check("old", false, "still wrong"), RegressionState.KnownFailure)),
            "master"
        );

        var (exit, _, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait", "--strict");

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Results_without_a_regression_summary_keep_the_old_rule_and_line()
    {
        var results = Results(RunStatus.Done, Check("a", false, "no"));

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(1, exit);
        Assert.Contains("1 of 1 checks failed.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("compared with", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_wait_it_posts_the_run_and_prints_its_id()
    {
        var (exit, output, error, server) = Run(
            (request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued)),
            "--tag",
            "regression"
        );

        Assert.Equal(0, exit);
        Assert.Equal($"version {VersionId}: new{Environment.NewLine}{RunId}{Environment.NewLine}", output);
        Assert.Empty(error);
        var (method, path, body) = Assert.Single(server.Requests);
        Assert.Equal(("POST", "/runs"), (method, path));
        using var sent = JsonDocument.Parse(body ?? "");
        Assert.Equal("example-agent", sent.RootElement.GetProperty("agent").GetString());
        Assert.Equal("regression", sent.RootElement.GetProperty("tag").GetString());
        // A version is never sent: the server reads it from the agent.
        Assert.False(sent.RootElement.TryGetProperty("agentVersionId", out _));
    }

    [Fact]
    public void An_endpoint_is_passed_on()
    {
        var (_, _, _, server) = Run(
            (request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued)),
            "--endpoint",
            "http://other:1"
        );

        using var sent = JsonDocument.Parse(server.Requests[0].Body ?? "");
        Assert.Equal("http://other:1", sent.RootElement.GetProperty("endpointUrl").GetString());
    }

    [Fact]
    public void The_version_of_the_run_is_printed_as_new_or_existing()
    {
        var (_, output, _, _) = Run(
            (request, _) =>
                request.Method == HttpMethod.Post
                    ? Json(
                        new RunCreated
                        {
                            RunId = RunId,
                            AgentVersionId = VersionId,
                            VersionCreated = false,
                        },
                        HttpStatusCode.Accepted
                    )
                    : Server(request, Summary(RunStatus.Done), Results(RunStatus.Done)),
            "--wait"
        );

        Assert.StartsWith($"version {VersionId}: existing{Environment.NewLine}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_version_option_is_gone()
    {
        var (exit, _, _, server) = Run(
            (request, _) => Server(request, Summary(RunStatus.Done), Results(RunStatus.Done)),
            "--version",
            Guid.NewGuid().ToString()
        );

        Assert.NotEqual(0, exit);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public void Without_wait_json_prints_the_run_id_as_json()
    {
        var (_, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Queued), Results(RunStatus.Queued)), "--json");

        using var printed = JsonDocument.Parse(output);
        Assert.Equal(RunId.ToString(), printed.RootElement.GetProperty("runId").GetString());
    }

    [Fact]
    public void Every_check_passing_exits_0_and_prints_the_totals_and_the_runs_page()
    {
        var results = Results(RunStatus.Done, Check("refund-called", true, "refund_order was called after get_order."));

        var (exit, output, _, server) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(0, exit);
        Assert.Equal(
            $"version {VersionId}: new{Environment.NewLine}1 of 1 checks passed.{Environment.NewLine}http://localhost:3230/runs/{RunId}{Environment.NewLine}",
            output
        );
        Assert.Contains(("GET", $"/runs/{RunId}/results", null), server.Requests);
    }

    [Fact]
    public void A_failing_check_exits_1_and_counts_it()
    {
        var results = Results(
            RunStatus.Done,
            Check("refund-called", true, "ok"),
            Check("refunded-event", false, "Event state.changed was not emitted.")
        );

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(1, exit);
        Assert.Contains("1 of 2 checks failed.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_judge_check_that_could_not_be_judged_fails_and_the_line_says_so()
    {
        var results = Results(
            RunStatus.Done,
            Check("refund-called", true, "ok"),
            Check("polite", false, "Not judged: the judge model could not be asked (boom).", notJudged: true)
        );

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(1, exit);
        Assert.Contains("1 of 2 checks failed (1 not judged: the judge model could not give a verdict).", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_check_under_its_samples_but_over_its_threshold_passes()
    {
        var results = Results(RunStatus.Done, Check("polite", true, "ok", threshold: 0.8, rate: 0.8));

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("1 of 1 checks passed.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_run_exits_2_with_the_reason()
    {
        var state = Summary(RunStatus.Failed, "the database went away");

        var (exit, output, _, _) = Run((request, _) => Server(request, state, Results(RunStatus.Failed) with { Run = state }), "--wait");

        Assert.Equal(2, exit);
        Assert.Contains("the database went away", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cancelled_run_exits_2()
    {
        var (exit, _, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Cancelled), Results(RunStatus.Cancelled)), "--wait");

        Assert.Equal(2, exit);
    }

    [Fact]
    public void Wait_with_json_prints_the_results_json_and_still_exits_by_the_checks()
    {
        var results = Results(RunStatus.Done, Check("c", false, "no"));

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait", "--json");

        Assert.Equal(1, exit);
        var printed = JsonSerializer.Deserialize<RunResults>(output, ContractJson.Options);
        Assert.Equal(results.Scenarios[0].Checks[0].Key, printed?.Scenarios[0].Checks[0].Key);
        Assert.Equal(RunStatus.Done, printed?.Run.Status);
    }

    [Fact]
    public void A_refusal_by_the_server_exits_2_with_its_reason()
    {
        var (exit, output, error, _) = Run(
            (_, _) =>
                new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(
                        /*lang=json,strict*/
                        """{"title":"x","mismatches":["check 'a': it names tool 'nope', which the version does not have"]}""",
                        Encoding.UTF8,
                        "application/json"
                    ),
                },
            "--wait"
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("409", error, StringComparison.Ordinal);
        Assert.Contains("tool 'nope'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_cannot_be_reached_exits_2()
    {
        var (exit, _, error, _) = Run((_, _) => throw new HttpRequestException("connection refused"), "--wait");

        Assert.Equal(2, exit);
        Assert.Contains("could not be reached", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_agent_file_exits_2_without_calling_the_server()
    {
        using var server = new FakeServer((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = CliApp.Run(
            ["run", "--dir", Path.Combine(Path.GetTempPath(), "no-such-suwageval-dir")],
            new StringReader(""),
            output,
            error,
            http,
            interrupt: TestContext.Current.CancellationToken
        );

        Assert.Equal(2, exit);
        Assert.Contains("agent.yaml", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    /// <summary>
    /// <c>run --wait</c> where Ctrl+C is pressed while the run is still going: the stand-in server cancels the interrupt the first
    /// time it is asked for the run's state, and answers that it is running; <paramref name="typed"/> is what the person answers.
    /// </summary>
    private static (int Exit, string Output, string Error, FakeServer Server) Interrupted(
        string typed,
        bool interactive,
        params string[] args
    )
    {
        using var interrupt = new CancellationTokenSource();
        var server = new FakeServer(
            (request, _) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    interrupt.Cancel();
                }
                return request.RequestUri?.AbsolutePath == $"/runs/{RunId}/cancel"
                    ? new HttpResponseMessage(HttpStatusCode.Accepted)
                    : Server(request, Summary(RunStatus.Running), Results(RunStatus.Running));
            }
        );
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(
            ["run", "--dir", Sample, "--wait", .. args],
            new StringReader(typed),
            output,
            error,
            http,
            interactive: interactive,
            interrupt: interrupt.Token
        );
        return (exit, output.ToString(), error.ToString(), server);
    }

    private static bool AskedToCancel(FakeServer server) =>
        server.Requests.Any(request => request == ("POST", $"/runs/{RunId}/cancel", null));

    [Fact]
    public void Ctrl_c_while_waiting_cancels_the_run_when_the_person_says_yes()
    {
        var (exit, _, error, server) = Interrupted("y\n", interactive: true);

        Assert.Equal(2, exit);
        Assert.Contains($"Cancel run {RunId} on the server too? [y/N]", error, StringComparison.Ordinal);
        Assert.Contains($"Cancelling run {RunId}.", error, StringComparison.Ordinal);
        Assert.True(AskedToCancel(server));
    }

    [Theory]
    [InlineData("n\n")]
    [InlineData("\n")]
    [InlineData("")]
    public void Ctrl_c_while_waiting_leaves_the_run_going_unless_the_person_says_yes(string typed)
    {
        var (exit, _, error, server) = Interrupted(typed, interactive: true);

        Assert.Equal(130, exit);
        Assert.Contains($"suwageval cancel {RunId}", error, StringComparison.Ordinal);
        Assert.False(AskedToCancel(server));
    }

    [Fact]
    public void Ctrl_c_with_nobody_to_ask_cancels_the_run_without_asking()
    {
        var (exit, _, error, server) = Interrupted("", interactive: false);

        Assert.Equal(2, exit);
        Assert.DoesNotContain("[y/N]", error, StringComparison.Ordinal);
        Assert.True(AskedToCancel(server));
    }

    [Fact]
    public void With_json_the_question_goes_to_the_error_stream_and_nothing_to_the_output()
    {
        var (_, output, error, _) = Interrupted("y\n", interactive: true, "--json");

        Assert.Empty(output);
        Assert.Contains("[y/N]", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_c_before_the_server_answered_says_the_run_may_exist()
    {
        using var server = new FakeServer((request, _) => Server(request, Summary(RunStatus.Running), Results(RunStatus.Running)));
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var interrupt = new CancellationTokenSource();
        interrupt.Cancel();

        var exit = CliApp.Run(["run", "--dir", Sample, "--wait"], new StringReader(""), output, error, http, interrupt: interrupt.Token);

        Assert.Equal(130, exit);
        Assert.Contains("suwageval runs", error.ToString(), StringComparison.Ordinal);
    }
}
