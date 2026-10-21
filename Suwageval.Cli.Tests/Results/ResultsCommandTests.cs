using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Results;

/// <summary>The command against a stand-in for the API, with the browser recorded instead of opened.</summary>
public sealed class ResultsCommandTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri?.AbsolutePath ?? "");
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, ContractJson.Options), Encoding.UTF8, "application/json"),
        };

    private static RunSummary Summary(RunStatus status) =>
        new()
        {
            Id = RunId,
            Agent = "example-agent",
            AgentVersionId = Guid.NewGuid(),
            EndpointUrl = "http://localhost:7010",
            Status = status,
            SamplesTotal = 1,
            SamplesDone = 1,
            ChecksPassed = 0,
            ChecksFailed = 1,
            CreatedAt = DateTime.UtcNow,
        };

    private static RunResults Failing(RunStatus status) =>
        new()
        {
            Run = Summary(status),
            Scenarios =
            [
                new ScenarioRunResult
                {
                    Key = "refund",
                    Tags = [],
                    Revision = 1,
                    DefinitionYaml = "",
                    Samples = [],
                    Checks =
                    [
                        new CheckRunResult
                        {
                            Key = "refund-called",
                            Revision = 1,
                            LatestRevision = 1,
                            JudgedByModel = false,
                            Definition = new TextCheck
                            {
                                Key = "refund-called",
                                ScenarioKey = "refund",
                                Contains = "x",
                            },
                            Passed = false,
                            PassRate = 0,
                            PassThreshold = 1,
                            Samples = [],
                        },
                    ],
                },
            ],
        };

    // The run is still going on the first ask, done on the next ones.
    private static Func<HttpRequestMessage, HttpResponseMessage> Api()
    {
        var asks = 0;
        return request =>
            request.RequestUri?.AbsolutePath switch
            {
                var path when path == $"/runs/{RunId}" => Json(Summary(asks++ == 0 ? RunStatus.Running : RunStatus.Done)),
                var path when path == $"/runs/{RunId}/results" => Json(Failing(RunStatus.Done)),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
    }

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) Results(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var opened = new List<Uri>();
        var exit = CliApp.Run(["results", .. args], new StringReader(""), output, error, http, opened.Add);
        return (exit, output.ToString(), error.ToString(), api, opened);
    }

    [Fact]
    public void Open_prints_and_opens_the_runs_page_on_the_web()
    {
        var (exit, output, error, _, opened) = Results(Api(), RunId.ToString(), "--open", "--server", "https://eval.example/");

        Assert.Equal(0, exit);
        Assert.Equal($"https://eval.example/runs/{RunId}{Environment.NewLine}", output);
        Assert.Empty(error);
        Assert.Equal([new Uri($"https://eval.example/runs/{RunId}")], opened);
    }

    [Fact]
    public void A_run_that_does_not_exist_exits_2_and_opens_nothing()
    {
        var (exit, output, error, _, opened) = Results(_ => new HttpResponseMessage(HttpStatusCode.NotFound), RunId.ToString(), "--open");

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void A_web_that_does_not_answer_exits_2()
    {
        var (exit, _, error, _, opened) = Results(_ => throw new HttpRequestException("connection refused"), RunId.ToString(), "--open");

        Assert.Equal(2, exit);
        Assert.Contains("could not be reached", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void Json_waits_for_the_end_prints_the_results_opens_nothing_and_exits_by_the_checks()
    {
        var (exit, output, _, api, opened) = Results(Api(), RunId.ToString(), "--json");

        Assert.Equal(1, exit);
        Assert.Equal(2, api.Paths.Count(path => path == $"/runs/{RunId}"));
        var printed = JsonSerializer.Deserialize<RunResults>(output, ContractJson.Options);
        Assert.Equal("refund-called", printed?.Scenarios[0].Checks[0].Key);
        Assert.Empty(opened);
    }

    [Fact]
    public void By_default_it_waits_for_the_end_and_prints_the_results_as_text_without_opening_anything()
    {
        var (exit, output, error, api, opened) = Results(Api(), RunId.ToString(), "--server", "https://eval.example/");

        Assert.Equal(1, exit);
        Assert.Empty(error);
        Assert.Empty(opened);
        Assert.Equal(2, api.Paths.Count(path => path == $"/runs/{RunId}"));
        Assert.Contains($"run {RunId}  done", output, StringComparison.Ordinal);
        Assert.Contains("tags -  branch -", output, StringComparison.Ordinal);
        Assert.Contains("1 of 1 checks failed.", output, StringComparison.Ordinal);
        Assert.Contains("failing checks:", output, StringComparison.Ordinal);
        Assert.Contains("refund / refund-called  0% passed, needs 100%", output, StringComparison.Ordinal);
        Assert.Contains("turn time: median -, p95 -", output, StringComparison.Ordinal);
        Assert.EndsWith($"https://eval.example/runs/{RunId}", output.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_finished_run_with_a_regression_summary_prints_what_run_wait_prints()
    {
        var withRegression = Failing(RunStatus.Done) with
        {
            MedianMs = 1200,
            P95Ms = 3000,
            Regression = new RegressionSummary
            {
                ComparedWith = "master",
                History = 3,
                Regressions = 0,
                New = 0,
                AlreadyBroken = 0,
                KnownFailures = 0,
                Fixed = 0,
                Unstable = 0,
                AgentErrors = 0,
            },
        };
        var (_, output, _, _, _) = Results(
            request => request.RequestUri?.AbsolutePath == $"/runs/{RunId}" ? Json(Summary(RunStatus.Done)) : Json(withRegression),
            RunId.ToString()
        );

        Assert.Contains("compared with master", output, StringComparison.Ordinal);
        Assert.Contains("1 checks: 0 passed, 1 failed", output, StringComparison.Ordinal);
        Assert.Contains("turn time: median 1.2s, p95 3.0s", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_and_open_together_are_refused()
    {
        var (exit, _, _, api, _) = Results(Api(), RunId.ToString(), "--json", "--open");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Paths);
    }

    [Fact]
    public void A_run_id_that_is_not_one_is_refused_before_any_request()
    {
        var (exit, _, _, api, _) = Results(Api(), "not-a-run");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Paths);
    }
}
