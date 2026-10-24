using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Evaluation.Runs;

/// <summary>The command against a stand-in for the API.</summary>
public sealed class RunsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static RunSummary Run(string id, string? tag) =>
        new()
        {
            Id = Guid.Parse(id),
            Agent = "example-agent",
            AgentVersionId = Guid.NewGuid(),
            Tags = tag is null ? [] : [tag],
            EndpointUrl = "http://localhost:7010",
            Status = RunStatus.Done,
            SamplesTotal = 1,
            SamplesDone = 1,
            ChecksPassed = 3,
            ChecksFailed = 1,
            CreatedAt = DateTime.Parse("2026-10-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
        };

    private static readonly RunSummary[] Newest =
    [
        Run("aaaaaaaa-0000-0000-0000-000000000003", "smoke") with
        {
            Reference = true,
            Origin = new RunOrigin
            {
                Branch = "master",
                Clean = true,
                Pushed = true,
            },
        },
        Run("aaaaaaaa-0000-0000-0000-000000000002", null) with
        {
            JudgeModels = ["judge-a", "judge-b"],
        },
        Run("aaaaaaaa-0000-0000-0000-000000000001", null) with
        {
            Reference = true,
        },
    ];

    private static (int Exit, string Output, string Error, List<string> Paths) Runs(params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["runs", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), paths);
    }

    private sealed class Answer(List<string> paths) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri?.PathAndQuery ?? "");
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri?.Query ?? "");
            var size = int.Parse(query["pageSize"] ?? "20", System.Globalization.CultureInfo.InvariantCulture);
            var number = int.Parse(query["page"] ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            var page = Newest.Skip((number - 1) * size).Take(size).ToList();
            var list = new RunList
            {
                Runs = page,
                Page = number,
                TotalCount = Newest.Length,
            };
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(list, ContractJson.Options), Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    [Fact]
    public void It_lists_the_runs_of_the_agent_in_agent_yaml_newest_first_with_their_checks()
    {
        var (exit, output, error, paths) = Runs();

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=10&grouping=None"], paths);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("aaaaaaaa-0000-0000-0000-000000000003  Done  smoke", lines[0], StringComparison.Ordinal);
        Assert.Contains("3/4 checks passed", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("branch master  reference", lines[0].TrimEnd(), StringComparison.Ordinal);
        Assert.EndsWith("branch -  judge judge-a, judge-b", lines[1].TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("judge", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Version_is_passed_on_to_the_server_and_can_be_repeated()
    {
        var first = "bbbbbbbb-0000-0000-0000-000000000001";
        var second = "bbbbbbbb-0000-0000-0000-000000000002";
        var (exit, _, _, paths) = Runs("--version", first, "--version", second);

        Assert.Equal(0, exit);
        Assert.Equal([$"/runs?agent=example-agent&page=1&pageSize=10&version={first}&version={second}&grouping=None"], paths);
    }

    [Fact]
    public void Tag_is_passed_on_to_the_server_can_be_repeated_and_goes_with_the_other_options()
    {
        var (exit, _, _, paths) = Runs("--tag", "smoke", "--tag", "billing", "--group", "sweep-1");

        Assert.Equal(0, exit);
        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=10&group=sweep-1&tag=smoke&tag=billing"], paths);
    }

    [Fact]
    public void Reference_lists_only_reference_runs()
    {
        var (_, output, _, _) = Runs("--reference");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.EndsWith("reference", line.TrimEnd(), StringComparison.Ordinal));
    }

    [Fact]
    public void Group_is_passed_on_to_the_server()
    {
        var (exit, _, _, paths) = Runs("--group", "sweep-1");

        Assert.Equal(0, exit);
        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=10&group=sweep-1"], paths);
    }

    [Fact]
    public void Runs_in_a_group_are_left_out_unless_any_group_or_all_runs_is_asked_for()
    {
        var (_, _, _, any) = Runs("--any-group");
        var (_, _, _, all) = Runs("--all-runs");

        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=10&grouping=Any"], any);
        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=10"], all);
    }

    [Fact]
    public void Group_any_group_and_all_runs_exclude_each_other_and_nothing_is_asked()
    {
        var (exit, _, _, paths) = Runs("--group", "sweep-1", "--any-group");

        Assert.NotEqual(0, exit);
        Assert.Empty(paths);
    }

    [Fact]
    public void Groups_lists_the_groups_in_use_and_with_archived_the_archived_ones_too()
    {
        RunGroupSummary[] groups =
        [
            new()
            {
                Name = "sweep-2",
                Runs = 3,
                Versions = 2,
                NewestRunAt = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Name = "sweep-1",
                Runs = 1,
                Versions = 1,
                NewestRunAt = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
                ArchivedAt = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
            },
        ];
        string[] Listed(params string[] args)
        {
            using var handler = new Fixed(JsonSerializer.Serialize(groups, ContractJson.Options));
            using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
            using var output = new StringWriter();
            using var error = new StringWriter();
            CliApp.Run(["runs", "--groups", .. args, "--dir", Sample], new StringReader(""), output, error, http);
            return output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        var active = Listed();
        var everything = Listed("--archived");

        Assert.Equal(["sweep-2  3 runs  2 versions  newest 2026-10-02 10:00Z"], active.Select(line => line.TrimEnd()));
        Assert.EndsWith("  archived", everything[1].TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(2, everything.Length);
    }

    private sealed class Fixed(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
            );
    }

    [Fact]
    public void Limit_keeps_the_newest_runs()
    {
        var (_, output, _, paths) = Runs("--limit", "2");

        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=2&grouping=None"], paths);
        Assert.Equal(2, output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("000000000001", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_limit_over_the_servers_page_size_is_asked_for_in_pages_of_at_most_that_size()
    {
        var (_, output, _, paths) = Runs("--limit", "150");

        Assert.Equal(["/runs?agent=example-agent&page=1&pageSize=100&grouping=None"], paths);
        Assert.Equal(3, output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Json_prints_the_run_summaries()
    {
        var (_, output, _, _) = Runs("--json");

        var printed = JsonSerializer.Deserialize<RunSummary[]>(output, ContractJson.Options);
        Assert.Equal(Newest.Select(run => run.Id), printed?.Select(run => run.Id));
    }
}
