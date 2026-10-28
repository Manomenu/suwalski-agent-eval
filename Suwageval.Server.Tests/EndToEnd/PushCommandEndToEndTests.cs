extern alias Cli;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Server.Tests.Evaluation.Runs;
using CliApp = Cli::Suwageval.Cli.CliApp;

namespace Suwageval.Server.Tests.EndToEnd;

/// <summary><c>suwageval push</c> from the CLI through the server in process to the test database and back.</summary>
public sealed class PushCommandEndToEndTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();
    private readonly string work = Directory.CreateTempSubdirectory("suwageval-push-").FullName;

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        Directory.Delete(work, recursive: true);
    }

    /// <summary>A client on the in-process server — or the test is skipped (failed in CI) without a database.</summary>
    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    /// <summary>The sample agent folder, copied, under a name of its own so tests do not meet in the shared database.</summary>
    private string Folder()
    {
        var dir = Path.Combine(work, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "scenarios"));
        File.WriteAllText(
            Path.Combine(dir, "agent.yaml"),
            File.ReadAllText(Path.Combine(Sample, "agent.yaml"))
                .Replace("name: example-agent", $"name: agent-{Guid.NewGuid():N}", StringComparison.Ordinal)
        );
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Sample, "scenarios")))
        {
            File.Copy(file, Path.Combine(dir, "scenarios", Path.GetFileName(file)));
        }
        return dir;
    }

    private static (int Exit, string Out, string Error) Run(HttpClient http, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(args, new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString());
    }

    private static int Count(string text, string word) =>
        text.Split('\n').Count(line => line.TrimEnd('\r').EndsWith(word, StringComparison.Ordinal));

    [Fact]
    public void The_first_push_creates_everything_and_the_second_changes_nothing()
    {
        using var http = Client();
        var dir = Folder();

        var (firstExit, firstOut, firstError) = Run(http, "push", dir);
        var (secondExit, secondOut, _) = Run(http, "push", dir);

        Assert.Equal(0, firstExit);
        Assert.Empty(firstError);
        // The agent, three scenarios, five checks; no version: that comes from the running agent when a run starts.
        Assert.Equal(9, Count(firstOut, ": created"));
        Assert.DoesNotContain("version", firstOut, StringComparison.Ordinal);
        Assert.Equal(0, secondExit);
        Assert.Equal(1, Count(secondOut, ": existing"));
        Assert.Equal(8, Count(secondOut, ": unchanged"));
    }

    [Fact]
    public void With_evaluation_off_the_agent_is_pushed_with_its_features_and_the_scenarios_stay_unsent_and_run_says_why()
    {
        using var http = Client();
        var dir = Folder();
        var agentFile = Path.Combine(dir, "agent.yaml");
        File.AppendAllText(agentFile, "features:\n  evaluation: false\n  monitoring:\n    environments: [production]\n");

        var (exit, output, _) = Run(http, "push", dir);
        var (agentExit, agentOut, _) = Run(http, "agent", "--dir", dir);
        File.WriteAllText(
            agentFile,
            File.ReadAllText(agentFile).Replace("evaluation: false", "evaluation: true", StringComparison.Ordinal)
        );
        var (runExit, _, runError) = Run(http, "run", "--dir", dir);

        Assert.Equal(0, exit);
        Assert.Contains("3 scenarios not pushed: evaluation is off", output, StringComparison.Ordinal);
        Assert.Equal(0, Count(output, ": created") - 1);
        Assert.Equal(0, agentExit);
        Assert.Contains(
            "features: evaluation off  monitoring on (close after 30m, environments production)",
            agentOut,
            StringComparison.Ordinal
        );
        Assert.Equal(2, runExit);
        Assert.Contains("features.evaluation", runError, StringComparison.Ordinal);
        Assert.Contains("switches evaluation on: `suwageval push` to apply it.", runError, StringComparison.Ordinal);
    }

    [Fact]
    public void A_changed_scenario_is_updated_and_the_rest_stays_unchanged()
    {
        using var http = Client();
        var dir = Folder();
        Run(http, "push", dir);
        var file = Path.Combine(dir, "scenarios", "refund-happy-path.yaml");
        File.WriteAllText(file, File.ReadAllText(file).Replace("I'd like a refund.", "Please refund it.", StringComparison.Ordinal));

        var (exit, output, _) = Run(http, "push", dir);

        Assert.Equal(0, exit);
        Assert.Contains("scenario refund-happy-path: updated", output, StringComparison.Ordinal);
        Assert.Equal(1, Count(output, ": updated"));
        Assert.Equal(7, Count(output, ": unchanged"));
    }

    [Fact]
    public void A_removed_scenario_file_deletes_the_scenario_and_its_checks_on_the_next_push()
    {
        using var http = Client();
        var dir = Folder();
        Run(http, "push", dir);
        File.Delete(Path.Combine(dir, "scenarios", "refund-happy-path.yaml"));

        var (exit, output, _) = Run(http, "push", dir);
        var (_, again, _) = Run(http, "push", dir);

        Assert.Equal(0, exit);
        Assert.Contains("scenario refund-happy-path: deleted", output, StringComparison.Ordinal);
        Assert.Contains("check refund-called-after-lookup: deleted", output, StringComparison.Ordinal);
        Assert.Contains("check refunded-state-event: deleted", output, StringComparison.Ordinal);
        Assert.Equal(3, Count(output, ": deleted"));
        Assert.Equal(0, Count(again, ": deleted"));
    }

    [Fact]
    public void Json_prints_one_object_with_the_outcomes()
    {
        using var http = Client();

        var (exit, output, _) = Run(http, "push", Folder(), "--json");

        Assert.Equal(0, exit);
        var result = JsonDocument.Parse(output).RootElement;
        Assert.True(result.GetProperty("agentCreated").GetBoolean());
        Assert.False(result.TryGetProperty("versionCreated", out _));
        Assert.Equal(3, result.GetProperty("scenarios").GetArrayLength());
        Assert.Equal(5, result.GetProperty("checks").GetArrayLength());
        Assert.All(
            result.GetProperty("checks").EnumerateArray(),
            check => Assert.Equal("created", check.GetProperty("outcome").GetString())
        );
    }

    [Fact]
    public void An_answer_the_server_refuses_is_exit_1_with_its_message()
    {
        using var http = Client();
        var dir = Folder();
        var agent = Path.Combine(dir, "agent.yaml");
        File.WriteAllText(agent, File.ReadAllText(agent).Replace("http://localhost:7010", "ftp://nowhere", StringComparison.Ordinal));

        var (exit, output, error) = Run(http, "push", dir);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Contains("422", error, StringComparison.Ordinal);
        Assert.Contains("endpointUrl", error, StringComparison.Ordinal);
    }
}
