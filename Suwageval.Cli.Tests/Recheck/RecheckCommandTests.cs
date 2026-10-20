using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Cli.Tests.Run;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Recheck;

/// <summary>The command against a stand-in for the server: the plan it asks for first, what it starts, prints and exits with.</summary>
public sealed class RecheckCommandTests
{
    private static readonly Guid SourceId = Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555");
    private static readonly Guid NewId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid VersionId = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private sealed class FakeServer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(string Method, string Path)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method.Method, request.RequestUri?.AbsolutePath ?? ""));
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, ContractJson.Options), Encoding.UTF8, "application/json"),
        };

    private static RunSummary Summary() =>
        new()
        {
            Id = NewId,
            Agent = "example-agent",
            AgentVersionId = VersionId,
            EndpointUrl = "http://localhost:7010",
            Status = RunStatus.Done,
            SamplesTotal = 1,
            SamplesDone = 1,
            ChecksPassed = 0,
            ChecksFailed = 0,
            CreatedAt = DateTime.UtcNow,
        };

    private static readonly RecheckPlan Possible = new()
    {
        Possible = true,
        ChangedChecks =
        [
            new ChangedCheck
            {
                ScenarioKey = "refund",
                CheckKey = "refund-called",
                Change = CheckChange.Changed,
                AfterJudgedByModel = false,
            },
        ],
        JudgedChecks = 0,
        SkippedScenarios = [new SkippedScenario { Key = "old", Reason = "The scenario was deleted after the run." }],
        Mismatches = [],
    };

    private static RecheckPlan Blocked(RecheckBlocker blocker) =>
        new()
        {
            Possible = false,
            Blocker = blocker,
            Reason = "Nothing to do.",
            ChangedChecks = [],
            JudgedChecks = 0,
            SkippedScenarios = [],
            Mismatches = [],
        };

    private static Func<HttpRequestMessage, HttpResponseMessage> Server(RecheckPlan plan) =>
        request =>
            (request.Method.Method, request.RequestUri?.AbsolutePath) switch
            {
                ("GET", var path) when path == $"/runs/{SourceId}/recheck" => Json(plan),
                ("POST", var path) when path == $"/runs/{SourceId}/recheck" => Json(
                    new RunCreated
                    {
                        RunId = NewId,
                        AgentVersionId = VersionId,
                        VersionCreated = false,
                    },
                    HttpStatusCode.Accepted
                ),
                ("GET", var path) when path == $"/runs/{NewId}" => Json(Summary()),
                ("GET", var path) when path == $"/runs/{NewId}/results" => Json(new RunResults { Run = Summary(), Scenarios = [] }),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };

    private static (int Exit, string Output, string Error, FakeServer Server) Run(RecheckPlan plan, params string[] args) =>
        Run(Server(plan), args);

    private static (int Exit, string Output, string Error, FakeServer Server) Run(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var server = new FakeServer(answer);
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["recheck", SourceId.ToString(), .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), server);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void Only_strict_fails_on_a_known_failure(bool strict, int expected)
    {
        var plan = Server(Possible);
        var (exit, _, _, _) = Run(
            request =>
                request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == $"/runs/{NewId}/results"
                    ? Json(KnownFailureResults.Create(Summary()))
                    : plan(request),
            strict ? ["--wait", "--strict"] : ["--wait"]
        );

        Assert.Equal(expected, exit);
    }

    [Fact]
    public void A_possible_plan_prints_the_skipped_scenarios_then_starts_the_recheck_and_prints_its_id()
    {
        var (exit, output, _, server) = Run(Possible);

        Assert.Equal(0, exit);
        Assert.Contains("skipped scenario 'old': The scenario was deleted after the run.", output, StringComparison.Ordinal);
        Assert.EndsWith($"{NewId}{Environment.NewLine}", output, StringComparison.Ordinal);
        Assert.Equal([("GET", $"/runs/{SourceId}/recheck"), ("POST", $"/runs/{SourceId}/recheck")], server.Requests);
    }

    [Fact]
    public void With_wait_it_prints_the_verdict_and_the_link()
    {
        var (exit, output, _, _) = Run(Possible, "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("0 of 0 checks passed.", output, StringComparison.Ordinal);
        Assert.Contains($"/runs/{NewId}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Dry_run_prints_the_plan_and_starts_nothing()
    {
        var (exit, output, _, server) = Run(Possible, "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains("changed: refund / refund-called", output, StringComparison.Ordinal);
        Assert.Contains("skipped scenario 'old'", output, StringComparison.Ordinal);
        Assert.Equal([("GET", $"/runs/{SourceId}/recheck")], server.Requests);
    }

    [Fact]
    public void Dry_run_with_json_prints_the_plan_as_json_even_when_it_is_not_possible()
    {
        var (exit, output, _, server) = Run(Blocked(RecheckBlocker.Mismatches), "--dry-run", "--json");

        Assert.Equal(0, exit);
        using var plan = JsonDocument.Parse(output);
        Assert.Equal("Mismatches", plan.RootElement.GetProperty("blocker").GetString());
        Assert.Single(server.Requests);
    }

    [Fact]
    public void When_nothing_changed_it_says_so_starts_nothing_and_exits_0()
    {
        var (exit, output, _, server) = Run(Blocked(RecheckBlocker.NothingChanged));

        Assert.Equal(0, exit);
        Assert.Contains("cannot be re-checked: Nothing to do.", output, StringComparison.Ordinal);
        Assert.Single(server.Requests);
    }

    [Theory]
    [InlineData(RecheckBlocker.NotFinished)]
    [InlineData(RecheckBlocker.AllSkipped)]
    [InlineData(RecheckBlocker.Mismatches)]
    public void Any_other_reason_not_to_start_exits_2(RecheckBlocker blocker)
    {
        var (exit, output, _, server) = Run(Blocked(blocker));

        Assert.Equal(2, exit);
        Assert.Contains("Nothing to do.", output, StringComparison.Ordinal);
        Assert.Single(server.Requests);
    }

    [Fact]
    public void A_refusal_of_the_server_is_printed_and_exits_2()
    {
        var (exit, _, error, _) = Run(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal(2, exit);
        Assert.Contains("404", error, StringComparison.Ordinal);
    }
}
