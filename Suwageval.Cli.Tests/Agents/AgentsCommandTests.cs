using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.Agents;

/// <summary>The command against a stand-in for the API; no agent.yaml is involved.</summary>
public sealed class AgentsCommandTests
{
    private static readonly Agent[] Found =
    [
        new()
        {
            Id = Guid.NewGuid(),
            Name = "example-agent",
            EndpointUrl = "http://localhost:7010",
            CreatedAt = DateTimeOffset.Parse("2026-10-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        },
    ];

    private sealed class Answer(List<string> paths, Agent[] answer) : HttpMessageHandler
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

    private static (int Exit, string Output, List<string> Paths) Agents(Agent[] answer, params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, answer);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["agents", .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), paths);
    }

    [Fact]
    public void It_lists_every_agent_with_its_endpoint()
    {
        var (exit, output, paths) = Agents(Found);

        Assert.Equal(0, exit);
        Assert.Equal(["/agents"], paths);
        Assert.Equal("example-agent  http://localhost:7010  created 2026-10-02 10:00Z", output.Trim());
    }

    [Fact]
    public void It_says_so_when_there_are_none()
    {
        var (_, output, _) = Agents([]);

        Assert.Equal("No agents.", output.Trim());
    }

    [Fact]
    public void Json_prints_the_agents()
    {
        var (_, output, _) = Agents(Found, "--json");

        Assert.Equal(Found, JsonSerializer.Deserialize<Agent[]>(output, ContractJson.Options));
    }
}
