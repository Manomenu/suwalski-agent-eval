using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Runs;

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
            Tag = tag,
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
        Run("aaaaaaaa-0000-0000-0000-000000000003", "smoke"),
        Run("aaaaaaaa-0000-0000-0000-000000000002", null),
        Run("aaaaaaaa-0000-0000-0000-000000000001", null),
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
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Newest, ContractJson.Options), Encoding.UTF8, "application/json"),
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
        Assert.Equal(["/runs?agent=example-agent"], paths);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("aaaaaaaa-0000-0000-0000-000000000003  Done  smoke", lines[0], StringComparison.Ordinal);
        Assert.Contains("3/4 checks passed", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Limit_keeps_the_newest_runs()
    {
        var (_, output, _, _) = Runs("--limit", "2");

        Assert.Equal(2, output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("000000000001", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_prints_the_run_summaries()
    {
        var (_, output, _, _) = Runs("--json");

        var printed = JsonSerializer.Deserialize<RunSummary[]>(output, ContractJson.Options);
        Assert.Equal(Newest.Select(run => run.Id), printed?.Select(run => run.Id));
    }
}
