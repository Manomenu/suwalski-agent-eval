using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Cli.Evaluation.Problems;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Evaluation.Problems;

/// <summary>The command against a stand-in for the API that answers the unstable checks and the agent errors.</summary>
public sealed class ProblemsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Version = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static readonly UnstableCheck[] Unstable =
    [
        new()
        {
            ScenarioKey = "refund",
            CheckKey = "says-sorry",
            CheckRevision = 1,
            AgentVersionId = Version,
            VersionLabel = "v2",
            Runs = 9,
            PassedRuns = 7,
            NewestFailedRunId = Guid.NewGuid(),
        },
    ];

    private static readonly AgentError[] Errors =
    [
        new()
        {
            ScenarioKey = "refund",
            AgentVersionId = Version,
            VersionLabel = "v2",
            Errors = 3,
            Samples = 12,
            NewestErrorRunId = Guid.NewGuid(),
            NewestError = "HTTP 500\nInternal Server Error",
        },
    ];

    private static (int Exit, string Output, string Error, List<string> Paths) Problems(
        UnstableCheck[] unstable,
        AgentError[] errors,
        params string[] args
    )
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, unstable, errors);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["problems", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), paths);
    }

    private sealed class Answer(List<string> paths, UnstableCheck[] unstable, AgentError[] errors) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? "";
            paths.Add(path);
            var body = path.Contains("/unstable-checks", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(unstable, ContractJson.Options)
                : JsonSerializer.Serialize(errors, ContractJson.Options);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
            );
        }
    }

    [Fact]
    public void It_lists_the_unstable_checks_and_the_agent_errors_under_their_headings()
    {
        var (exit, output, error, paths) = Problems(Unstable, Errors);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(["/eval/agents/example-agent/unstable-checks", "/eval/agents/example-agent/agent-errors"], paths);
        Assert.Equal(
            string.Join(
                '\n',
                "Unstable checks (a failure there may be noise):",
                $"  refund/says-sorry  7/9  version {Version} (v2)",
                "Agent errors (no answer, so the checks were not evaluated):",
                $"  refund  3/12  version {Version} (v2)  HTTP 500 Internal Server Error"
            ),
            output.Trim().Replace("\r\n", "\n", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Only_the_kind_that_has_problems_is_listed()
    {
        var (_, output, _, _) = Problems([], Errors);

        Assert.DoesNotContain("Unstable checks", output, StringComparison.Ordinal);
        Assert.Contains("Agent errors", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_and_data_version_pass_to_both_requests()
    {
        var other = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        var (exit, _, _, paths) = Problems(
            Unstable,
            Errors,
            "--version",
            Version.ToString(),
            "--version",
            other.ToString(),
            "--data-version",
            "index 7"
        );

        Assert.Equal(0, exit);
        Assert.Equal(
            [
                $"/eval/agents/example-agent/unstable-checks?version={Version}&version={other}&dataVersion=index%207",
                $"/eval/agents/example-agent/agent-errors?version={Version}&version={other}&dataVersion=index%207",
            ],
            paths
        );
    }

    [Fact]
    public void It_says_so_when_there_is_no_problem()
    {
        var (exit, output, _, _) = Problems([], []);

        Assert.Equal(0, exit);
        Assert.Equal("No unstable checks and no agent errors.", output.Trim());
    }

    [Fact]
    public void Json_prints_both_lists()
    {
        var (_, output, _, _) = Problems(Unstable, Errors, "--json");

        var printed = JsonSerializer.Deserialize<ProblemsCommand.Found>(output, ContractJson.Options);
        Assert.NotNull(printed);
        Assert.Equal(Unstable, printed.UnstableChecks);
        Assert.Equal(Errors, printed.AgentErrors);
    }
}
