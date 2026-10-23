using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Cli.Compare;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Compare;

/// <summary>The command against a stand-in for the API, with the browser recorded instead of opened.</summary>
public sealed class CompareCommandTests
{
    private static readonly Guid First = Guid.Parse("11111111-2222-3333-4444-555555555551");
    private static readonly Guid Second = Guid.Parse("11111111-2222-3333-4444-555555555552");
    private static readonly Guid Version = Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555");

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> PathsAndQueries { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            PathsAndQueries.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(answer(request));
        }
    }

    private static RunSummary Summary(Guid id, int checksPassed, int checksFailed, string? label = null) =>
        new()
        {
            Id = id,
            Agent = "example-agent",
            AgentVersionId = Version,
            Tags = ["nightly"],
            EndpointUrl = "http://localhost:7010",
            Status = RunStatus.Done,
            SamplesTotal = 4,
            SamplesDone = 4,
            SamplesFailed = 1,
            ChecksPassed = checksPassed,
            ChecksFailed = checksFailed,
            CreatedAt = DateTime.UtcNow,
            Label = label,
        };

    private static VariantRun Column(Guid id, string? versionLabel, int checksPassed, int checksFailed, string? runLabel = null) =>
        new()
        {
            Run = Summary(id, checksPassed, checksFailed, runLabel),
            VersionLabel = versionLabel,
            ModelNames = ["gpt-4o"],
            MedianMs = 1500,
            P95Ms = 3000,
        };

    private static ComparedRun Compared(Guid id, string? judge, bool judgeChanged, double? passRate, TimeChange? median = null) =>
        new()
        {
            Run = Summary(id, 1, 0),
            JudgeModel = judge,
            JudgeChanged = judgeChanged,
            PassRate = passRate,
            MedianMs = 1200,
            P95Ms = 3000,
            MedianChange = median,
        };

    private static ComparedCell Cell(bool passed, int samplesPassed, Contracts.Runs.Trend? trend, string? judge, int revision) =>
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

    private static ComparedCheck Check(string key, bool differs, bool comparable, params ComparedCell?[] cells) =>
        new()
        {
            ScenarioKey = "greeting",
            ScenarioTags = [],
            CheckKey = key,
            Definition = new Contracts.Checks.TextCheck { Key = "k", ScenarioKey = "s" },
            JudgedByModel = false,
            Differs = differs,
            Comparable = comparable,
            Cells = cells,
        };

    private static BaselineComparison Baseline(Guid id) =>
        new()
        {
            RunId = id,
            Runs =
            [
                Compared(First, "judge-a", false, 0.5),
                Compared(Second, "judge-b", true, 0.75, new TimeChange(-300, Contracts.Runs.Trend.Better)),
            ],
            Tags =
            [
                new ComparedTag
                {
                    Rates = [new ComparedRate(0.5), new ComparedRate(0.75, new RateChange(25, Contracts.Runs.Trend.Better))],
                },
                new ComparedTag
                {
                    Tag = "smoke",
                    Rates = [new ComparedRate(1), new ComparedRate(0.5, new RateChange(-50, Contracts.Runs.Trend.Worse))],
                },
            ],
            Checks =
            [
                Check("polite", true, true, Cell(false, 1, null, "judge-a", 1), Cell(true, 3, Contracts.Runs.Trend.Better, "judge-b", 2)),
                Check("short", true, false, Cell(true, 3, null, null, 1), null),
                Check("same", false, true, Cell(true, 3, null, null, 1), Cell(true, 3, Contracts.Runs.Trend.Same, null, 1)),
            ],
        };

    private static RunComparison Comparison(Guid? baseline) =>
        new()
        {
            Agent = "example-agent",
            Group = "sweep-1",
            Runs = [Column(First, "gpt-4o", 75, 22) with { JudgeModel = "judge-a" }, Column(Second, "gpt-4o", 80, 17, "relabelled")],
            Tags =
            [
                new ComparedTag { Tag = null, Rates = [new ComparedRate(0.77), new ComparedRate(0.82)] },
                new ComparedTag { Tag = "billing", Rates = [new ComparedRate(0.5), new ComparedRate()] },
            ],
            Scenarios =
            [
                new VariantScenario
                {
                    Key = "refund",
                    Tags = ["billing"],
                    Cells =
                    [
                        new VariantCell
                        {
                            ChecksPassed = 3,
                            ChecksTotal = 4,
                            FailedChecks = ["refund-event"],
                            Checks =
                            [
                                new VariantCheck
                                {
                                    Key = "refund-event",
                                    Passed = false,
                                    PassRate = 0,
                                    Samples = 1,
                                    SamplesPassed = 0,
                                    JudgedByModel = true,
                                    Failures = [],
                                },
                            ],
                            Samples = 1,
                            SamplesFailed = 0,
                            NoAnswers = [],
                        },
                        null,
                    ],
                },
            ],
            Baseline = baseline is { } id ? Baseline(id) : null,
        };

    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var query = request.RequestUri?.Query ?? "";
        var baseline = query.Contains($"baseline={First}", StringComparison.Ordinal) ? First : (Guid?)null;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(Comparison(baseline), ContractJson.Options),
                Encoding.UTF8,
                "application/json"
            ),
        };
    }

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) Compare(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        Action<Uri>? open,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var opened = new List<Uri>();
        var exit = CliApp.Run(["compare", .. args, "--dir", Sample], new StringReader(""), output, error, http, open ?? opened.Add);
        return (exit, output.ToString(), error.ToString(), api, opened);
    }

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) Print(params string[] args) =>
        Compare(Answer, null, args);

    [Fact]
    public void Runs_are_printed_as_a_lettered_column_each_with_checks_per_scenario_and_the_failed_ones_and_no_changes()
    {
        var (exit, output, error, api, opened) = Print(First.ToString(), Second.ToString());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Empty(opened);
        Assert.Equal([$"/runs/compare?runs={First}&runs={Second}"], api.PathsAndQueries);
        Assert.Contains("comparison of example-agent (group sweep-1), 2 runs", output, StringComparison.Ordinal);
        Assert.Contains("B  relabelled  models", output, StringComparison.Ordinal);
        Assert.Contains(
            "A  gpt-4o  models gpt-4o  run 11111111  checks 75/97 passed, 22 failed  samples 3/4 answered",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains("billing  50%", output, StringComparison.Ordinal);
        Assert.Contains("refund    3/4", output, StringComparison.Ordinal);
        Assert.Contains("A failed: refund-event (model-judged)", output, StringComparison.Ordinal);
        Assert.Contains("  judge judge-a", output, StringComparison.Ordinal);
        Assert.Contains("page: http://localhost:3230/compare?runs=", output, StringComparison.Ordinal);
        Assert.DoesNotContain("baseline", output, StringComparison.Ordinal);
        Assert.DoesNotContain("pts", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_baseline_is_asked_for_and_adds_the_changes_against_it()
    {
        var (exit, output, _, api, _) = Print(First.ToString(), Second.ToString(), "--baseline", First.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/runs/compare?runs={First}&runs={Second}&baseline={First}"], api.PathsAndQueries);
        Assert.Contains("A baseline  gpt-4o  models", output, StringComparison.Ordinal);
        Assert.Contains("A baseline  judge judge-a  pass 50%", output, StringComparison.Ordinal);
        Assert.Contains("B  judge judge-b (judge differs from baseline)  pass 75%", output, StringComparison.Ordinal);
        Assert.Contains("median 1.2s (-0.3s faster)", output, StringComparison.Ordinal);
        Assert.Contains("All    50%", output, StringComparison.Ordinal);
        Assert.Contains("75% (+25 pts ▲)", output, StringComparison.Ordinal);
        Assert.Contains("50% (-50 pts ▼)", output, StringComparison.Ordinal);
        Assert.Contains("greeting / polite", output, StringComparison.Ordinal);
        Assert.Contains("A  failed 1/3 (33%)", output, StringComparison.Ordinal);
        Assert.Contains("B  passed 3/3 (100%) ▲  judge judge-b  revision 2 (baseline 1)", output, StringComparison.Ordinal);
        Assert.Contains("greeting / short  (not comparable", output, StringComparison.Ordinal);
        Assert.DoesNotContain("greeting / same", output, StringComparison.Ordinal);
        Assert.Contains("1 check is the same in every run.", output, StringComparison.Ordinal);
        Assert.Contains($"&baseline={First}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_group_is_asked_for_by_name_and_the_agent_of_agent_yaml_and_takes_a_baseline_too()
    {
        var (exit, output, _, api, _) = Print("--group", "sweep-1", "--baseline", First.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/runs/compare?group=sweep-1&agent=example-agent&baseline={First}"], api.PathsAndQueries);
        Assert.Contains("comparison of example-agent (group sweep-1)", output, StringComparison.Ordinal);
        Assert.Contains("against the baseline:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_prints_the_comparison_record()
    {
        var (exit, output, _, _, opened) = Print(First.ToString(), Second.ToString(), "--baseline", First.ToString(), "--json");

        Assert.Equal(0, exit);
        Assert.Empty(opened);
        var parsed = JsonSerializer.Deserialize<RunComparison>(output, ContractJson.Options);
        Assert.Equal(2, parsed?.Runs.Count);
        Assert.Equal(First, parsed?.Baseline?.RunId);
        Assert.Equal(3, parsed?.Baseline?.Checks.Count);
    }

    [Fact]
    public void Json_and_open_cannot_be_combined()
    {
        var (exit, _, _, api, _) = Print(First.ToString(), Second.ToString(), "--json", "--open");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.PathsAndQueries);
    }

    [Fact]
    public void Open_opens_the_page_with_the_runs_in_the_order_given_and_the_baseline()
    {
        var (exit, output, error, api, opened) = Compare(
            Answer,
            null,
            Second.ToString(),
            First.ToString(),
            "--baseline",
            First.ToString(),
            "--open",
            "--server",
            "https://eval.example/"
        );

        var page = new Uri($"https://eval.example/compare?runs={Second},{First}&baseline={First}");
        Assert.Equal(0, exit);
        Assert.Equal($"{page}{Environment.NewLine}", output);
        Assert.Empty(error);
        Assert.Equal([page], opened);
        Assert.Equal([$"/runs/compare?runs={Second}&runs={First}&baseline={First}"], api.PathsAndQueries);
    }

    [Fact]
    public void Open_with_print_opens_the_page_that_opens_the_print_dialog()
    {
        var (exit, _, error, _, opened) = Print(First.ToString(), Second.ToString(), "--baseline", First.ToString(), "--open", "--print");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal([new Uri($"http://localhost:3230/compare?runs={First},{Second}&baseline={First}&print=1")], opened);
    }

    [Fact]
    public void Print_without_open_is_refused_before_anything_is_asked()
    {
        var (exit, output, error, api, opened) = Print(First.ToString(), Second.ToString(), "--print");

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("--print needs --open", error, StringComparison.Ordinal);
        Assert.Empty(api.PathsAndQueries);
        Assert.Empty(opened);
    }

    [Fact]
    public void Print_of_more_runs_than_a_page_holds_is_refused_before_anything_is_asked()
    {
        var ids = Enumerable
            .Range(1, CompareCommand.PrintMaxRuns + 1)
            .Select(n => Guid.Parse($"{n:D8}-0000-0000-0000-000000000000"))
            .ToArray();

        var (exit, output, error, api, opened) = Print([.. ids.Select(id => id.ToString()), "--open", "--print"]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains(
            $"{ids.Length} runs do not fit a printed page (at most {CompareCommand.PrintMaxRuns})",
            error,
            StringComparison.Ordinal
        );
        Assert.Empty(api.PathsAndQueries);
        Assert.Empty(opened);
    }

    [Fact]
    public void Print_of_a_group_larger_than_a_page_is_refused_before_the_page_is_opened()
    {
        // The group's size is known only from the answer: the answer here has more columns than a page holds.
        var runs = Enumerable.Range(0, CompareCommand.PrintMaxRuns + 1).Select(_ => Comparison(null).Runs[0]).ToArray();
        var (exit, output, error, _, opened) = Compare(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(Comparison(null) with { Runs = runs }, ContractJson.Options),
                    Encoding.UTF8,
                    "application/json"
                ),
            },
            null,
            "--group",
            "sweep-1",
            "--open",
            "--print"
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("do not fit a printed page", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void Open_for_a_group_opens_its_page_with_the_agent()
    {
        var (exit, _, _, _, opened) = Print("--group", "sweep-1", "--open");

        Assert.Equal(0, exit);
        Assert.Equal([new Uri("http://localhost:3230/compare?group=sweep-1&agent=example-agent")], opened);
    }

    [Fact]
    public void A_run_that_does_not_exist_exits_2_and_opens_nothing()
    {
        var (exit, output, error, _, opened) = Compare(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            null,
            First.ToString(),
            Second.ToString(),
            "--open"
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void A_web_that_does_not_answer_exits_2()
    {
        var (exit, _, error, _, opened) = Compare(
            _ => throw new HttpRequestException("connection refused"),
            null,
            First.ToString(),
            Second.ToString(),
            "--open"
        );

        Assert.Equal(2, exit);
        Assert.Contains("could not be reached", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void Without_a_browser_the_link_is_printed_and_the_exit_is_0()
    {
        var (exit, output, error, _, _) = Compare(
            Answer,
            _ => throw new Win32Exception("no opener"),
            First.ToString(),
            Second.ToString(),
            "--open"
        );

        Assert.Equal(0, exit);
        Assert.Contains("/compare?runs=", output, StringComparison.Ordinal);
        Assert.Contains("No browser could be opened", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ids_and_a_group_together_or_neither_are_refused_before_any_request()
    {
        var (bothExit, _, _, bothApi, _) = Print(First.ToString(), "--group", "sweep-1");
        var (neitherExit, _, _, neitherApi, _) = Print();

        Assert.NotEqual(0, bothExit);
        Assert.NotEqual(0, neitherExit);
        Assert.Empty(bothApi.PathsAndQueries);
        Assert.Empty(neitherApi.PathsAndQueries);
    }

    [Fact]
    public void A_baseline_that_is_not_among_the_runs_or_has_no_other_run_to_compare_is_refused_before_any_request()
    {
        var (outsideExit, _, _, outsideApi, _) = Print(First.ToString(), Second.ToString(), "--baseline", Guid.NewGuid().ToString());
        var (aloneExit, _, _, aloneApi, _) = Print(First.ToString(), "--baseline", First.ToString());

        Assert.NotEqual(0, outsideExit);
        Assert.NotEqual(0, aloneExit);
        Assert.Empty(outsideApi.PathsAndQueries);
        Assert.Empty(aloneApi.PathsAndQueries);
    }

    [Fact]
    public void More_than_twenty_runs_are_refused_before_any_request()
    {
        var ids = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid().ToString()).ToArray();

        var (exit, _, _, api, _) = Print(ids);

        Assert.NotEqual(0, exit);
        Assert.Empty(api.PathsAndQueries);
    }

    [Fact]
    public void An_id_that_is_not_a_run_id_is_refused_before_any_request()
    {
        var (exit, _, _, api, _) = Print(First.ToString(), "not-a-run");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.PathsAndQueries);
    }
}
