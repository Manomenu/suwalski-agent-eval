using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
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

    private static CheckRunResult Check(string key, bool passed, string message, double threshold = 1.0, double rate = -1) =>
        new()
        {
            Key = key,
            Revision = 1,
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

    private static (int Exit, string Output, string Error, FakeServer Server) Run(
        Func<HttpRequestMessage, string?, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var server = new FakeServer(answer);
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["run", "--dir", Sample, .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), server);
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
        Assert.Contains($"version {VersionId}, tag regression", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_contract_that_changed_during_the_run_and_undeclared_output_are_shown()
    {
        var changed = Results(RunStatus.Done, Check("c", true, "ok"));
        changed = changed with
        {
            Run = changed.Run with { ContractChangedDuringRun = true },
            Scenarios =
            [
                changed.Scenarios[0] with
                {
                    Samples = [changed.Scenarios[0].Samples[0] with { Undeclared = ["tool 'cancel_order'"] }],
                },
            ],
        };

        var (_, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), changed), "--wait");

        Assert.Contains("the agent's contract changed while the run was going", output, StringComparison.Ordinal);
        Assert.Contains("sample 1 produced what its version does not declare: tool 'cancel_order'", output, StringComparison.Ordinal);
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
    public void Every_check_passing_exits_0_and_prints_each_with_its_pass_rate()
    {
        var results = Results(RunStatus.Done, Check("refund-called", true, "refund_order was called after get_order."));

        var (exit, output, _, server) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("refund-happy-path", output, StringComparison.Ordinal);
        Assert.Contains("✓ refund-called  100% (1/1 samples, needs 100%)", output, StringComparison.Ordinal);
        Assert.Contains("1 of 1 checks passed.", output, StringComparison.Ordinal);
        Assert.Contains(("GET", $"/runs/{RunId}/results", null), server.Requests);
    }

    [Fact]
    public void A_failing_check_exits_1_and_prints_its_message()
    {
        var results = Results(
            RunStatus.Done,
            Check("refund-called", true, "ok"),
            Check("refunded-event", false, "Event state.changed was not emitted.")
        );

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(1, exit);
        Assert.Contains("✗ refunded-event  0%", output, StringComparison.Ordinal);
        Assert.Contains("Event state.changed was not emitted.", output, StringComparison.Ordinal);
        Assert.Contains("1 of 2 checks passed.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_check_under_its_samples_but_over_its_threshold_passes()
    {
        var results = Results(RunStatus.Done, Check("polite", true, "ok", threshold: 0.8, rate: 0.8));

        var (exit, output, _, _) = Run((request, _) => Server(request, Summary(RunStatus.Done), results), "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("80% (1/1 samples, needs 80%)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_run_exits_2_with_the_reason()
    {
        var state = Summary(RunStatus.Failed, "the database went away");

        var (exit, output, _, _) = Run((request, _) => Server(request, state, Results(RunStatus.Failed)), "--wait");

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
            http
        );

        Assert.Equal(2, exit);
        Assert.Contains("agent.yaml", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }
}
