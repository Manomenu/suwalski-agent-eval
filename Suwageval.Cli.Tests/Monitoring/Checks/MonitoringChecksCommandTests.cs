using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Cli.Monitoring.Checks;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Monitoring;

namespace Suwageval.Cli.Tests.Monitoring.Checks;

/// <summary><c>suwageval monitoring checks</c> and <c>trend</c> against a stand-in for the API.</summary>
public sealed class MonitoringChecksCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Version = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static (int Exit, string Output, List<string> Paths) Run(object answer, params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, answer);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["monitoring", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString() + error, paths);
    }

    private sealed class Answer(List<string> paths, object answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(answer, ContractJson.Options), Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    [Fact]
    public void The_checks_are_listed_with_their_rates()
    {
        MonitoringCheck[] checks =
        [
            new()
            {
                Revision = 2,
                CreatedAt = DateTimeOffset.UnixEpoch,
                Check = new ToolNotCalledCheck
                {
                    Key = "no-refund",
                    Tool = "refund",
                    Description = "Never refunds on its own.",
                },
                Last7Days = new CheckRate(4, 5),
                Last30Days = new CheckRate(0, 0),
            },
        ];

        var (exit, output, paths) = Run(checks, "checks");

        Assert.Equal(0, exit);
        Assert.Equal(["/monitoring/agents/example-agent/checks"], paths);
        Assert.Equal("no-refund  7 days 80% (4/5)  30 days -  Never refunds on its own.", output.Trim());
    }

    [Fact]
    public void The_environment_goes_to_the_server()
    {
        var (_, _, paths) = Run(Array.Empty<MonitoringCheck>(), "checks", "--environment", "production");

        Assert.Equal(["/monitoring/agents/example-agent/checks?environment=production"], paths);
    }

    [Fact]
    public void No_checks_says_where_they_come_from()
    {
        var (exit, output, _) = Run(Array.Empty<MonitoringCheck>(), "checks");

        Assert.Equal(0, exit);
        Assert.Contains("monitoring-checks.yaml", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_trend_asks_for_the_days_and_environment_and_prints_day_by_day()
    {
        var trend = new MonitoringTrend
        {
            Days = [new MonitoringDay("2026-10-24", "production", 2, 2), new MonitoringDay("2026-10-24", "staging", 1, 0)],
            Points = [new MonitoringPoint("2026-10-24", "no-refund", Version, 1, 2)],
            Versions = [new VersionChange(DateTimeOffset.Parse("2026-10-24T08:00:00Z", CultureInfo.InvariantCulture), Version, "v7")],
            DataVersions = [],
        };

        var (exit, output, paths) = Run(trend, "trend", "--days", "7", "--environment", "production");

        Assert.Equal(0, exit);
        Assert.Equal(["/monitoring/agents/example-agent/trend?days=7&environment=production"], paths);
        var lines = output.Trim().ReplaceLineEndings("\n").Split('\n');
        Assert.Equal("2026-10-24  3 conversations (production 2, staging 1)  2 checked  no-refund 50% (1/2)", lines[0]);
        Assert.StartsWith($"version {Version}", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Days_out_of_range_are_an_error_before_any_request()
    {
        var (exit, output, paths) = Run(Array.Empty<MonitoringCheck>(), "trend", "--days", "0");

        Assert.NotEqual(0, exit);
        Assert.Contains("--days is from 1 to 365.", output, StringComparison.Ordinal);
        Assert.Empty(paths);
    }

    [Fact]
    public void An_unused_check_has_no_rate() => Assert.Equal("-", MonitoringChecksCommand.Rate(new CheckRate(0, 0)));

    [Fact]
    public void Checks_open_the_monitoring_page_of_the_environment()
    {
        var (exit, output, error, opened, paths) = OpenedPage.Run(
            "[]",
            "monitoring",
            "checks",
            "--environment",
            "production",
            "--dir",
            OpenedPage.Sample
        );

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal([new Uri("https://eval.example/agents/example-agent/monitoring?environment=production")], opened);
        Assert.Equal("https://eval.example/agents/example-agent/monitoring?environment=production" + Environment.NewLine, output);
        Assert.Empty(paths);
    }

    [Fact]
    public void Trend_opens_the_monitoring_page_of_the_agent()
    {
        var (exit, output, error, opened, paths) = OpenedPage.Run("{}", "monitoring", "trend", "--dir", OpenedPage.Sample);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal([new Uri("https://eval.example/agents/example-agent/monitoring")], opened);
        Assert.Equal("https://eval.example/agents/example-agent/monitoring" + Environment.NewLine, output);
        Assert.Empty(paths);
    }
}
