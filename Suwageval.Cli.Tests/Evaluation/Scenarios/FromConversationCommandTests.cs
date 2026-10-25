using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Tests.Evaluation.Scenarios;

/// <summary><c>suwageval scenario from-conversation</c> against a stand-in for the API.</summary>
public sealed class FromConversationCommandTests : IDisposable
{
    private static readonly Guid Id = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly ScenarioDraft Draft = new()
    {
        Key = "conversation-chat-81",
        Path = "suwageval/scenarios/conversation-chat-81.yaml",
        Yaml = "key: conversation-chat-81\nconversation:\n- user: Hi\n",
    };

    private readonly string dir = Directory.CreateTempSubdirectory("suwageval-draft-").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private static (int Exit, string Output, string Error, List<string> Paths) Run(params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["scenario", "from-conversation", Id.ToString(), .. args], new StringReader(""), output, error, http);
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
                    Content = new StringContent(JsonSerializer.Serialize(Draft, ContractJson.Options), Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    [Fact]
    public void The_draft_is_printed_and_where_it_goes_is_said_apart()
    {
        var (exit, output, error, paths) = Run("--dir", dir);

        Assert.Equal(0, exit);
        Assert.Equal([$"/monitoring/conversations/{Id}/scenario"], paths);
        Assert.Equal(Draft.Yaml, output);
        Assert.Contains("save it as suwageval/scenarios/conversation-chat-81.yaml", error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_message_to_copy_up_to_goes_to_the_server_and_a_wrong_one_is_refused_before_it()
    {
        var (user, _, _, userPaths) = Run("--dir", dir, "--up-to-user", "2");
        var (agent, _, _, agentPaths) = Run("--dir", dir, "--up-to-agent", "1");
        var (zero, _, zeroError, zeroPaths) = Run("--dir", dir, "--up-to-user", "0");
        var (both, _, bothError, bothPaths) = Run("--dir", dir, "--up-to-user", "1", "--up-to-agent", "1");

        Assert.Equal((0, 0), (user, agent));
        Assert.Equal([$"/monitoring/conversations/{Id}/scenario?upToUserMessage=2"], userPaths);
        Assert.Equal([$"/monitoring/conversations/{Id}/scenario?upToAgentMessage=1"], agentPaths);
        Assert.NotEqual(0, zero);
        Assert.Contains("--up-to-user is a user message of the conversation, from 1.", zeroError, StringComparison.Ordinal);
        Assert.NotEqual(0, both);
        Assert.Contains("not both", bothError, StringComparison.Ordinal);
        Assert.Empty(zeroPaths);
        Assert.Empty(bothPaths);
    }

    [Fact]
    public void The_simulated_rest_and_its_turns_go_to_the_server_only_with_a_cut()
    {
        var (simulated, _, _, simulatedPaths) = Run("--dir", dir, "--up-to-user", "2", "--simulate", "--turns", "4");
        var (noCut, _, noCutError, noCutPaths) = Run("--dir", dir, "--simulate");
        var (noSimulate, _, noSimulateError, noSimulatePaths) = Run("--dir", dir, "--up-to-user", "2", "--turns", "4");

        Assert.Equal(0, simulated);
        Assert.Equal([$"/monitoring/conversations/{Id}/scenario?upToUserMessage=2&simulate=true&turns=4"], simulatedPaths);
        Assert.NotEqual(0, noCut);
        Assert.Contains("--simulate needs --up-to-user or --up-to-agent", noCutError, StringComparison.Ordinal);
        Assert.NotEqual(0, noSimulate);
        Assert.Contains("--turns needs --simulate.", noSimulateError, StringComparison.Ordinal);
        Assert.Empty(noCutPaths);
        Assert.Empty(noSimulatePaths);
    }

    [Fact]
    public void With_write_the_file_is_written_into_the_scenarios_folder_and_never_over_one_that_is_there()
    {
        var (first, firstOutput, _, _) = Run("--dir", dir, "--write");
        var file = Path.Combine(dir, "scenarios", "conversation-chat-81.yaml");
        File.AppendAllText(file, "# edited\n");
        var (second, _, secondError, _) = Run("--dir", dir, "--write");

        Assert.Equal(0, first);
        Assert.Contains("wrote ", firstOutput, StringComparison.Ordinal);
        Assert.Equal(1, second);
        Assert.Contains("exists: nothing written", secondError, StringComparison.Ordinal);
        Assert.EndsWith("# edited\n", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_with_a_key_still_shows_that_scenario()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = CliApp.Run(
            ["scenario", "--help"],
            new StringReader(""),
            output,
            error,
            interrupt: TestContext.Current.CancellationToken
        );

        Assert.Equal(0, exit);
        Assert.Contains("from-conversation", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("<key>", output.ToString(), StringComparison.Ordinal);
    }
}
