using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Unstable;

/// <summary>The command against a stand-in for the API.</summary>
public sealed class UnstableCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Version = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static readonly UnstableCheck[] Found =
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

    private static (int Exit, string Output, string Error, List<string> Paths) Unstable(UnstableCheck[] answer, params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, answer);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["unstable", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), paths);
    }

    private sealed class Answer(List<string> paths, UnstableCheck[] answer) : HttpMessageHandler
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
    public void It_lists_the_unstable_checks_of_the_agent_in_agent_yaml()
    {
        var (exit, output, error, paths) = Unstable(Found);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(["/agents/example-agent/unstable-checks"], paths);
        Assert.Equal($"refund/says-sorry  7/9  version {Version} (v2)", output.Trim());
    }

    [Fact]
    public void Version_passes_the_versions_to_the_server()
    {
        var other = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        var (exit, _, _, paths) = Unstable(Found, "--version", Version.ToString(), "--version", other.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/agents/example-agent/unstable-checks?version={Version}&version={other}"], paths);
    }

    [Fact]
    public void It_says_so_when_every_check_is_stable()
    {
        var (exit, output, _, _) = Unstable([]);

        Assert.Equal(0, exit);
        Assert.Equal("No unstable checks.", output.Trim());
    }

    [Fact]
    public void Json_prints_the_unstable_checks()
    {
        var (_, output, _, _) = Unstable(Found, "--json");

        var printed = JsonSerializer.Deserialize<UnstableCheck[]>(output, ContractJson.Options);
        Assert.Equal(Found, printed);
    }
}
