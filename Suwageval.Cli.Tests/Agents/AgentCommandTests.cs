using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.Agents;

/// <summary>The command against a stand-in for the API, with the sample agent folder as the repository.</summary>
public sealed class AgentCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly AgentDetail Found = new()
    {
        Id = Guid.NewGuid(),
        Name = "example-agent",
        EndpointUrl = "http://localhost:7010",
        CreatedAt = DateTimeOffset.Parse("2026-10-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        Versions = 2,
        Scenarios = 5,
        Checks = 9,
        Samples = 4,
        Regression = new RegressionSettings { Branches = ["master", "release"], History = 7 },
    };

    private sealed class Answer(List<string> paths) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Found, ContractJson.Options), Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private static (int Exit, string Output, List<string> Paths) Agent(params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["agent", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), paths);
    }

    [Fact]
    public void It_prints_what_the_server_stores_for_the_agent()
    {
        var (exit, output, paths) = Agent();

        Assert.Equal(0, exit);
        Assert.Equal(["/agents/example-agent"], paths);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("example-agent  http://localhost:7010  created 2026-10-02 10:00Z", lines[0]);
        Assert.Equal("2 versions  5 scenarios  9 checks  samples 4", lines[1]);
        Assert.Equal("regression: branches master, release  history 7", lines[2]);
    }

    [Fact]
    public void Json_prints_the_record()
    {
        var (exit, output, _) = Agent("--json");

        Assert.Equal(0, exit);
        Assert.Equal(9, JsonSerializer.Deserialize<AgentDetail>(output, ContractJson.Options)?.Checks);
    }
}
