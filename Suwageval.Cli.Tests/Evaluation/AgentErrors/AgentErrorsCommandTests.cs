using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Evaluation.AgentErrors;

/// <summary>The command against a stand-in for the API.</summary>
public sealed class AgentErrorsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Version = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static readonly AgentError[] Found =
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

    private static (int Exit, string Output, string Error, List<string> Paths) AgentErrors(AgentError[] answer, params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, answer);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["agent-errors", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), paths);
    }

    private sealed class Answer(List<string> paths, AgentError[] answer) : HttpMessageHandler
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
    public void It_lists_the_agent_errors_of_the_agent_in_agent_yaml()
    {
        var (exit, output, error, paths) = AgentErrors(Found);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(["/eval/agents/example-agent/agent-errors"], paths);
        Assert.Equal($"refund  3/12  version {Version} (v2)  HTTP 500 Internal Server Error", output.Trim());
    }

    [Fact]
    public void Version_passes_the_versions_to_the_server()
    {
        var (exit, _, _, paths) = AgentErrors(Found, "--version", Version.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/eval/agents/example-agent/agent-errors?version={Version}"], paths);
    }

    [Fact]
    public void Data_version_passes_the_data_versions_to_the_server()
    {
        var (exit, _, _, paths) = AgentErrors(Found, "--version", Version.ToString(), "--data-version", "index-7");

        Assert.Equal(0, exit);
        Assert.Equal([$"/eval/agents/example-agent/agent-errors?version={Version}&dataVersion=index-7"], paths);
    }

    [Fact]
    public void It_says_so_when_the_agent_always_answered()
    {
        var (exit, output, _, _) = AgentErrors([]);

        Assert.Equal(0, exit);
        Assert.Equal("No agent errors.", output.Trim());
    }

    [Fact]
    public void Json_prints_the_agent_errors()
    {
        var (_, output, _, _) = AgentErrors(Found, "--json");

        Assert.Equal(Found, JsonSerializer.Deserialize<AgentError[]>(output, ContractJson.Options));
    }
}
