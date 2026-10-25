using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Evaluation.Sweep;

/// <summary>The command against a stand-in for the server, the agent and the shell.</summary>
public sealed class SweepCommandTests : IDisposable
{
    private const string Section =
        "sweep:\n  variantsDir: setups\n  reconfigure: reconfigure {variant}\n  restore: restore\n  waitSeconds: 1\n";

    private static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("suwageval-sweep-");
    private readonly List<string> commands = [];
    private readonly List<string> notes = [];
    private string label = "default";
    private string? servedLabel;
    private string typed = "";
    private bool interactive;

    public void Dispose() => folder.Delete(recursive: true);

    private string Agent(string sweep, params string[] variants)
    {
        File.WriteAllText(Path.Combine(folder.FullName, "agent.yaml"), $"name: example-agent\nendpointUrl: http://agent.test\n{sweep}");
        Directory.CreateDirectory(Path.Combine(folder.FullName, "setups"));
        foreach (var variant in variants)
        {
            File.WriteAllText(Path.Combine(folder.FullName, "setups", variant + ".env"), "");
        }
        return folder.FullName;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, ContractJson.Options), Encoding.UTF8, "application/json"),
        };

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var summary = new RunSummary
        {
            Id = RunId,
            Agent = "example-agent",
            AgentVersionId = RunId,
            Tags = [],
            EndpointUrl = "http://agent.test",
            Status = RunStatus.Done,
            SamplesTotal = 0,
            SamplesDone = 0,
            ChecksPassed = 0,
            ChecksFailed = 0,
            CreatedAt = DateTime.UtcNow,
        };
        return (request.Method.Method, request.RequestUri?.AbsolutePath) switch
        {
            ("GET", "/suwageval/contract") => Json(
                new AgentContract
                {
                    Label = servedLabel ?? label,
                    Agents =
                    [
                        new LlmAgentDefinition
                        {
                            Name = "main",
                            Model = new ModelDefinition { Name = "m" },
                            Config = "",
                        },
                    ],
                }
            ),
            ("POST", "/eval/runs") => Json(
                new RunCreated
                {
                    RunId = RunId,
                    AgentVersionId = RunId,
                    VersionCreated = false,
                }
            ),
            ("PUT", { } path) when path.EndsWith("/note", StringComparison.Ordinal) => RecordNote(path, request),
            ("GET", var path) when path == $"/eval/runs/{RunId}" => Json(summary),
            ("GET", var path) when path == $"/eval/runs/{RunId}/results" => Json(new RunResults { Run = summary, Scenarios = [] }),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage RecordNote(string path, HttpRequestMessage request)
    {
        notes.Add($"{path} {request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}");
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private Task<int> Shell(string commandLine, TextWriter output, CancellationToken cancellationToken)
    {
        commands.Add(commandLine);
        if (commandLine.StartsWith("reconfigure ", StringComparison.Ordinal))
        {
            label = commandLine["reconfigure ".Length..];
        }
        return Task.FromResult(0);
    }

    private (int Exit, string Output, string Error) Sweep(string dir, params string[] args)
    {
        using var handler = new Handler(Answer);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(
            ["sweep", "--dir", dir, .. args],
            new StringReader(typed),
            output,
            error,
            http,
            interactive: interactive,
            origins: new RunOriginReader((_, _) => null, _ => null),
            shell: Shell,
            labelPollInterval: TimeSpan.FromMilliseconds(10)
        );
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Every_variant_of_the_folder_is_run_in_one_group_and_the_compare_link_is_printed()
    {
        var dir = Agent(Section, "fast", "slow");

        var (exit, output, _) = Sweep(dir, "--group", "sweep-test");

        Assert.Equal(0, exit);
        Assert.Equal(["reconfigure fast", "reconfigure slow", "restore"], commands);
        Assert.Contains("/compare?group=sweep-test&agent=example-agent", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_note_is_set_once_on_the_group_after_its_first_run_is_created_and_a_too_long_one_is_refused()
    {
        var dir = Agent(Section, "fast", "slow");

        var (exit, _, _) = Sweep(dir, "--group", "sweep-test", "--note", " trying two setups ");
        var refused = Sweep(dir, "--note", new string('x', 501));

        Assert.Equal(0, exit);
        Assert.Equal(["/eval/agents/example-agent/run-groups/sweep-test/note {\"note\":\"trying two setups\"}"], notes);
        Assert.NotEqual(0, refused.Exit);
        Assert.Single(notes);
    }

    [Fact]
    public void Named_variants_are_run_and_an_unknown_one_is_refused_before_anything_runs()
    {
        var dir = Agent(Section, "fast", "slow");

        Assert.Equal(0, Sweep(dir, "slow").Exit);
        Assert.Equal(["reconfigure slow", "restore"], commands);

        commands.Clear();
        var (exit, _, error) = Sweep(dir, "turbo");
        Assert.Equal(2, exit);
        Assert.Contains("No such variant: turbo", error, StringComparison.Ordinal);
        Assert.Empty(commands);
    }

    [Fact]
    public void More_variants_than_one_comparison_holds_are_refused_before_anything_runs()
    {
        var names = Enumerable.Range(1, ComparisonLimit.MaxRuns + 1).Select(n => $"v{n}").ToArray();
        var dir = Agent(Section, names);

        var (exit, _, error) = Sweep(dir);

        Assert.Equal(2, exit);
        Assert.Contains($"A sweep runs at most {ComparisonLimit.MaxRuns} variants", error, StringComparison.Ordinal);
        Assert.Empty(commands);
        Assert.Equal(0, Sweep(dir, names[0]).Exit);
    }

    [Fact]
    public void Select_runs_the_variants_picked_from_the_list_and_asks_again_after_a_bad_answer()
    {
        var dir = Agent(Section, "fast", "slow", "turbo");
        interactive = true;
        typed = "9\n1,3\n";

        var (exit, output, error) = Sweep(dir, "--select");

        Assert.Equal(0, exit);
        Assert.Equal(["reconfigure fast", "reconfigure turbo", "restore"], commands);
        Assert.Contains("2) slow", output, StringComparison.Ordinal);
        Assert.Contains("'9' is outside 1-3", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_together_with_named_variants_is_refused()
    {
        interactive = true;

        var (exit, _, error) = Sweep(Agent(Section, "fast"), "--select", "fast");

        Assert.Equal(2, exit);
        Assert.Contains("not both", error, StringComparison.Ordinal);
        Assert.Empty(commands);
    }

    [Fact]
    public void Select_without_a_terminal_is_refused_and_says_to_name_the_variants()
    {
        var (exit, _, error) = Sweep(Agent(Section, "fast"), "--select");

        Assert.Equal(2, exit);
        Assert.Contains("name the variants instead", error, StringComparison.Ordinal);
        Assert.Empty(commands);
    }

    [Fact]
    public void A_variant_whose_label_never_shows_up_is_a_row_and_exits_2()
    {
        var dir = Agent(Section, "fast");
        servedLabel = "stuck";

        var (exit, output, _) = Sweep(dir);

        Assert.Equal(2, exit);
        Assert.Contains("label is 'stuck', not 'fast'", output, StringComparison.Ordinal);
        Assert.Equal("restore", commands[^1]);
    }

    [Fact]
    public void An_agent_without_a_sweep_section_is_refused()
    {
        var (exit, _, error) = Sweep(Agent("", "fast"));

        Assert.Equal(2, exit);
        Assert.Contains("no 'sweep:' section", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sweep:\n  variantsDir: setups\n", "reconfigure")]
    [InlineData("sweep:\n  variantsDir: setups\n  reconfigure: x\n  waitSeconds: 0\n", "waitSeconds")]
    [InlineData("sweep:\n  variantsDir: setups\n  reconfigure: x\n  befor: y\n", "befor")]
    public void A_sweep_section_that_is_wrong_is_a_problem_at_its_place(string section, string mentioned)
    {
        var dir = Agent(section);
        var file = Path.Combine(dir, "agent.yaml");
        var problems = new List<Problem>();

        var agent = AgentFileReader.Read(file, File.ReadAllText(file), problems);

        Assert.Null(agent);
        Assert.Contains(
            problems,
            problem =>
                problem.Path.Contains(mentioned, StringComparison.Ordinal) || problem.Message.Contains(mentioned, StringComparison.Ordinal)
        );
    }
}
