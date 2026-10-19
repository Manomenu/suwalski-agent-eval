using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Compare;

/// <summary>The command against a stand-in for the API, with the browser recorded instead of opened.</summary>
public sealed class CompareCommandTests
{
    private static readonly Guid First = Guid.Parse("11111111-2222-3333-4444-555555555551");
    private static readonly Guid Second = Guid.Parse("11111111-2222-3333-4444-555555555552");
    private static readonly Guid Third = Guid.Parse("11111111-2222-3333-4444-555555555553");

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri?.AbsolutePath ?? "");
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Summary(Guid id, string agent) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new RunSummary
                    {
                        Id = id,
                        Agent = agent,
                        AgentVersionId = Guid.NewGuid(),
                        EndpointUrl = "http://localhost:7010",
                        Status = RunStatus.Done,
                        SamplesTotal = 1,
                        SamplesDone = 1,
                        ChecksPassed = 1,
                        ChecksFailed = 0,
                        CreatedAt = DateTime.UtcNow,
                    },
                    ContractJson.Options
                ),
                Encoding.UTF8,
                "application/json"
            ),
        };

    private static HttpResponseMessage OfAgent(HttpRequestMessage request, Func<Guid, string> agentOf) =>
        Guid.TryParse(request.RequestUri?.AbsolutePath.Split('/').Last(), out var id)
            ? Summary(id, agentOf(id))
            : new HttpResponseMessage(HttpStatusCode.NotFound);

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) Compare(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        Action<Uri>? open = null,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var opened = new List<Uri>();
        var exit = CliApp.Run(["compare", "--open", .. args], new StringReader(""), output, error, http, open ?? opened.Add);
        return (exit, output.ToString(), error.ToString(), api, opened);
    }

    private static readonly Guid Version = Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555");

    private static ComparedRun Compared(Guid id, string? judge, bool judgeChanged, double? passRate, TimeChange? median = null) =>
        new()
        {
            Run = new RunSummary
            {
                Id = id,
                Agent = "support-bot",
                AgentVersionId = Version,
                Tag = "nightly",
                EndpointUrl = "http://localhost:7010",
                Status = RunStatus.Done,
                SamplesTotal = 1,
                SamplesDone = 1,
                ChecksPassed = 1,
                ChecksFailed = 0,
                CreatedAt = DateTime.UtcNow,
            },
            JudgeModel = judge,
            JudgeChanged = judgeChanged,
            PassRate = passRate,
            MedianMs = 1200,
            P95Ms = 3000,
            MedianChange = median,
        };

    private static ComparedCell Cell(bool passed, int samplesPassed, Suwageval.Contracts.Runs.Trend? trend, string? judge, int revision) =>
        new()
        {
            Passed = passed,
            PassRate = samplesPassed / 3.0,
            SamplesPassed = samplesPassed,
            Samples = 3,
            NotJudged = 0,
            Revision = revision,
            LatestRevision = 2,
            JudgedByModel = judge is not null,
            JudgeModel = judge,
            Trend = trend,
        };

    private static RunComparison Comparison() =>
        new()
        {
            Runs =
            [
                Compared(First, "judge-a", false, 0.5),
                Compared(Second, "judge-b", true, 0.75, new TimeChange(-300, Suwageval.Contracts.Runs.Trend.Better)),
            ],
            Tags =
            [
                new ComparedTag
                {
                    Rates = [new ComparedRate(0.5), new ComparedRate(0.75, new RateChange(25, Suwageval.Contracts.Runs.Trend.Better))],
                },
                new ComparedTag
                {
                    Tag = "smoke",
                    Rates = [new ComparedRate(1), new ComparedRate(0.5, new RateChange(-50, Suwageval.Contracts.Runs.Trend.Worse))],
                },
            ],
            Checks =
            [
                new ComparedCheck
                {
                    ScenarioKey = "greeting",
                    CheckKey = "polite",
                    Definition = new Suwageval.Contracts.Checks.TextCheck { Key = "k", ScenarioKey = "s" },
                    JudgedByModel = true,
                    Differs = true,
                    Comparable = true,
                    Cells = [Cell(false, 1, null, "judge-a", 1), Cell(true, 3, Suwageval.Contracts.Runs.Trend.Better, "judge-b", 2)],
                },
                new ComparedCheck
                {
                    ScenarioKey = "greeting",
                    CheckKey = "short",
                    Definition = new Suwageval.Contracts.Checks.TextCheck { Key = "k", ScenarioKey = "s" },
                    JudgedByModel = false,
                    Differs = true,
                    Comparable = false,
                    Cells = [Cell(true, 3, null, null, 1), null],
                },
                new ComparedCheck
                {
                    ScenarioKey = "greeting",
                    CheckKey = "same",
                    Definition = new Suwageval.Contracts.Checks.TextCheck { Key = "k", ScenarioKey = "s" },
                    JudgedByModel = false,
                    Differs = false,
                    Comparable = true,
                    Cells = [Cell(true, 3, null, null, 1), Cell(true, 3, Suwageval.Contracts.Runs.Trend.Same, null, 1)],
                },
            ],
        };

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) Print(params string[] args)
    {
        var api = new FakeApi(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(Comparison(), ContractJson.Options), Encoding.UTF8, "application/json"),
        });
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var opened = new List<Uri>();
        var exit = CliApp.Run(["compare", .. args], new StringReader(""), output, error, http, opened.Add);
        return (exit, output.ToString(), error.ToString(), api, opened);
    }

    [Fact]
    public void By_default_it_prints_the_comparison_from_one_request_and_opens_nothing()
    {
        var (exit, output, error, api, opened) = Print(First.ToString(), Second.ToString());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Empty(opened);
        Assert.Equal(["/runs/compare"], api.Paths);
        Assert.Contains(
            "#1 baseline  run 11111111  version aaaaaaaa  tag nightly  judge judge-a  pass 50%",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains("#2  run 11111111", output, StringComparison.Ordinal);
        Assert.Contains("judge judge-b (judge differs from baseline)  pass 75%", output, StringComparison.Ordinal);
        Assert.Contains("median 1.2s (-0.3s faster)", output, StringComparison.Ordinal);
        Assert.Contains("All    50%", output, StringComparison.Ordinal);
        Assert.Contains("75% (+25 pts ▲)", output, StringComparison.Ordinal);
        Assert.Contains("50% (-50 pts ▼)", output, StringComparison.Ordinal);
        Assert.Contains("greeting / polite", output, StringComparison.Ordinal);
        Assert.Contains("#1  failed 1/3 (33%)", output, StringComparison.Ordinal);
        Assert.Contains("#2  passed 3/3 (100%) ▲  judge judge-b  revision 2 (baseline 1)", output, StringComparison.Ordinal);
        Assert.Contains("greeting / short  (not comparable", output, StringComparison.Ordinal);
        Assert.DoesNotContain("greeting / same", output, StringComparison.Ordinal);
        Assert.Contains("1 check is the same in every run.", output, StringComparison.Ordinal);
        Assert.Contains("/compare?runs=", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_prints_the_comparison_record()
    {
        var (exit, output, _, _, opened) = Print(First.ToString(), Second.ToString(), "--json");

        Assert.Equal(0, exit);
        Assert.Empty(opened);
        var parsed = JsonSerializer.Deserialize<RunComparison>(output, ContractJson.Options);
        Assert.Equal(2, parsed?.Runs.Count);
        Assert.Equal(3, parsed?.Checks.Count);
    }

    [Fact]
    public void Json_and_open_cannot_be_combined()
    {
        var (exit, _, _, api, _) = Print(First.ToString(), Second.ToString(), "--json", "--open");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Paths);
    }

    [Fact]
    public void It_prints_and_opens_the_comparison_with_the_runs_in_the_order_given()
    {
        var (exit, output, error, api, opened) = Compare(
            request => OfAgent(request, _ => "support-bot"),
            null,
            Second.ToString(),
            First.ToString(),
            Third.ToString(),
            "--server",
            "https://eval.example/"
        );

        var page = new Uri($"https://eval.example/compare?runs={Second},{First},{Third}");
        Assert.Equal(0, exit);
        Assert.Equal($"{page}{Environment.NewLine}", output);
        Assert.Empty(error);
        Assert.Equal([page], opened);
        Assert.Equal([$"/runs/{Second}", $"/runs/{First}", $"/runs/{Third}"], api.Paths);
    }

    [Fact]
    public void A_run_that_does_not_exist_exits_2_and_opens_nothing()
    {
        var (exit, output, error, _, opened) = Compare(
            request =>
                request.RequestUri?.AbsolutePath.EndsWith(Second.ToString(), StringComparison.Ordinal) == true
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : OfAgent(request, _ => "support-bot"),
            null,
            First.ToString(),
            Second.ToString()
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void Runs_of_different_agents_exit_2_and_open_nothing()
    {
        var (exit, output, error, _, opened) = Compare(
            request => OfAgent(request, id => id == First ? "support-bot" : "sales-bot"),
            null,
            First.ToString(),
            Second.ToString()
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("Runs of different agents cannot be compared", error, StringComparison.Ordinal);
        Assert.Contains("support-bot", error, StringComparison.Ordinal);
        Assert.Contains("sales-bot", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void A_web_that_does_not_answer_exits_2()
    {
        var (exit, _, error, _, opened) = Compare(
            _ => throw new HttpRequestException("connection refused"),
            null,
            First.ToString(),
            Second.ToString()
        );

        Assert.Equal(2, exit);
        Assert.Contains("could not be reached", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void Without_a_browser_the_link_is_printed_and_the_exit_is_0()
    {
        var (exit, output, error, _, _) = Compare(
            request => OfAgent(request, _ => "support-bot"),
            _ => throw new Win32Exception("no opener"),
            First.ToString(),
            Second.ToString()
        );

        Assert.Equal(0, exit);
        Assert.Contains("/compare?runs=", output, StringComparison.Ordinal);
        Assert.Contains("No browser could be opened", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public void Fewer_than_two_or_more_than_five_runs_are_refused_before_any_request(int count)
    {
        var ids = Enumerable
            .Range(0, count)
            .Select(index => Guid.Parse($"11111111-2222-3333-4444-55555555556{index}").ToString())
            .ToArray();

        var (exit, _, _, api, opened) = Compare(request => OfAgent(request, _ => "support-bot"), null, ids);

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Paths);
        Assert.Empty(opened);
    }

    [Fact]
    public void The_same_run_twice_is_not_two_runs()
    {
        var (exit, _, _, api, _) = Compare(request => OfAgent(request, _ => "support-bot"), null, First.ToString(), First.ToString());

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Paths);
    }

    [Fact]
    public void An_id_that_is_not_a_run_id_is_refused_before_any_request()
    {
        var (exit, _, _, api, _) = Compare(request => OfAgent(request, _ => "support-bot"), null, First.ToString(), "not-a-run");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Paths);
    }
}
