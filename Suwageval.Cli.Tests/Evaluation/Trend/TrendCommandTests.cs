using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Evaluation.Trend;

/// <summary>The command against a stand-in for the API.</summary>
public sealed class TrendCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Newer = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Older = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // Newest first, the way the server sends it.
    private static readonly RunPassRates[] Points =
    [
        new()
        {
            RunId = Newer,
            CreatedAt = DateTime.Parse("2026-10-03T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
            AgentVersionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            VersionLabel = "v2",
            ModelNames = [],
            PassRate = 0.75,
            MedianMs = 1200,
            P95Ms = 4000,
            Tags = [new TagPassRate("smoke", 1.0), new TagPassRate("refunds", 0.5)],
        },
        new()
        {
            RunId = Older,
            CreatedAt = DateTime.Parse("2026-10-02T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
            AgentVersionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            ModelNames = [],
            PassRate = 0.5,
            Tags = [],
        },
    ];

    private sealed class Answer(List<string> paths, RunPassRates[] answer) : HttpMessageHandler
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

    private static (int Exit, string Output, List<string> Paths) Trend(RunPassRates[] answer, params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, answer);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["trend", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), paths);
    }

    [Fact]
    public void It_prints_one_line_per_run_oldest_first()
    {
        var (exit, output, paths) = Trend(Points);

        Assert.Equal(0, exit);
        Assert.Equal(["/eval/agents/example-agent/pass-rates"], paths);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal(
            $"{Older}  2026-10-02 09:00Z  version bbbbbbbb-0000-0000-0000-000000000001  50% passed  median -  p95 -",
            lines[0].TrimEnd()
        );
        Assert.Equal(
            $"{Newer}  2026-10-03 09:00Z  version v2  75% passed  median 1.2s  p95 4.0s  tags: smoke 100%, refunds 50%",
            lines[1].TrimEnd()
        );
    }

    [Fact]
    public void It_says_so_when_no_run_finished()
    {
        var (_, output, _) = Trend([]);

        Assert.Equal("No finished runs.", output.Trim());
    }

    [Fact]
    public void Json_prints_the_points_as_the_server_sent_them()
    {
        var (_, output, _) = Trend(Points, "--json");

        var printed = JsonSerializer.Deserialize<RunPassRates[]>(output, ContractJson.Options);
        Assert.Equal(Points.Select(point => point.RunId), printed?.Select(point => point.RunId));
        Assert.Equal(Points[0].Tags, printed?[0].Tags);
    }
}
