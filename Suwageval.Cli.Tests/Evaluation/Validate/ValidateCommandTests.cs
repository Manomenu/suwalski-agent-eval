using System.Net;
using System.Text.Json;
using Suwageval.Cli.Tests.Validation;

namespace Suwageval.Cli.Tests.Evaluation.Validate;

public sealed class ValidateCommandTests : IDisposable
{
    private readonly string work = Directory.CreateTempSubdirectory("suwageval-cli-").FullName;

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    public void Dispose() => Directory.Delete(work, recursive: true);

    /// <summary>Against an agent that serves <see cref="TestAgent.Contract"/>.</summary>
    private static (int Exit, string Out, string Error) Run(string stdin, params string[] args)
    {
        using var agent = TestAgent.AgentServing(TestAgent.Contract);
        return Run(agent, stdin, args);
    }

    private static (int Exit, string Out, string Error) Run(HttpClient agent, string stdin, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(args, new StringReader(stdin), output, error, agent);
        return (exit, output.ToString(), error.ToString());
    }

    private string Folder(string agent, params (string Name, string Text)[] scenarios)
    {
        var dir = Path.Combine(work, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "scenarios"));
        File.WriteAllText(Path.Combine(dir, "agent.yaml"), agent);
        foreach (var (name, text) in scenarios)
        {
            File.WriteAllText(Path.Combine(dir, "scenarios", name), text);
        }
        return dir;
    }

    [Fact]
    public void The_sample_folder_is_valid()
    {
        var (exit, output, error) = Run("", "validate", Sample);

        Assert.Equal(0, exit);
        Assert.Equal($"valid (3 scenarios, 5 checks){Environment.NewLine}", output);
        Assert.Empty(error);
    }

    [Fact]
    public void A_folders_monitoring_checks_are_validated_with_its_scenarios()
    {
        var dir = Folder(TestAgent.Yaml, ("s.yaml", TestAgent.Scenario));
        File.WriteAllText(
            Path.Combine(dir, "monitoring-checks.yaml"),
            "checks:\n  - key: no-wire\n    type: tool-not-called\n    tool: wire_money\n    description: Never wires money.\n"
        );

        var (exit, output, error) = Run("", "validate", dir);

        Assert.Equal(1, exit);
        Assert.Contains("wire_money", output + error, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_path_the_suwageval_folder_of_the_working_directory_is_used()
    {
        var previous = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetDirectoryName(Sample)!;
        try
        {
            Assert.Equal(0, Run("", "validate").Exit);
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }
    }

    [Fact]
    public void One_scenario_file_is_checked_against_the_agent_in_dir()
    {
        var file = Path.Combine(Sample, "scenarios", "refund-follow-up.yaml");

        Assert.Equal(0, Run("", "validate", file, "--dir", Sample).Exit);
    }

    [Fact]
    public void A_problem_is_printed_with_file_position_and_path_and_exits_one()
    {
        var dir = Folder(TestAgent.Yaml, ("a.yaml", "key: Bad\nconversation:\n  - user: hi\n"));

        var (exit, output, _) = Run("", "validate", dir);

        Assert.Equal(1, exit);
        var expected = Path.Combine(dir, "scenarios", "a.yaml") + ":1:1: key: The scenario key 'Bad' is not lowercase kebab-case";
        Assert.StartsWith(expected, output, StringComparison.Ordinal);
        Assert.EndsWith($"1 problem{Environment.NewLine}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_without_an_agent_file_is_a_problem()
    {
        var dir = Path.Combine(work, "empty");
        Directory.CreateDirectory(dir);

        var (exit, output, _) = Run("", "validate", dir);

        Assert.Equal(1, exit);
        Assert.Contains("agent.yaml", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_that_does_not_exist_is_a_problem()
    {
        var (exit, output, _) = Run("", "validate", Path.Combine(work, "nowhere"));

        Assert.Equal(1, exit);
        Assert.Contains("No such file or folder", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dash_reads_one_scenario_from_stdin()
    {
        var (exit, output, _) = Run(TestAgent.Scenario, "validate", "-", "--dir", Folder(TestAgent.Yaml));

        Assert.Equal(0, exit);
        Assert.Equal($"valid (1 scenarios, 1 checks){Environment.NewLine}", output);
    }

    [Fact]
    public void Stdin_problems_are_named_stdin()
    {
        var (exit, output, _) = Run(
            TestAgent.Scenario.Replace("refund_order", "nope", StringComparison.Ordinal),
            "validate",
            "-",
            "--dir",
            Folder(TestAgent.Yaml)
        );

        Assert.Equal(1, exit);
        Assert.StartsWith("<stdin>:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_output_has_valid_and_the_problems()
    {
        var dir = Folder(TestAgent.Yaml, ("a.yaml", "key: Bad\nconversation:\n  - user: hi\n"));

        var (exit, output, _) = Run("", "validate", dir, "--json");

        Assert.Equal(1, exit);
        using var document = JsonDocument.Parse(output);
        Assert.False(document.RootElement.GetProperty("valid").GetBoolean());
        var problem = Assert.Single(document.RootElement.GetProperty("problems").EnumerateArray());
        Assert.Equal(Path.Combine(dir, "scenarios", "a.yaml"), problem.GetProperty("file").GetString());
        Assert.Equal("key", problem.GetProperty("path").GetString());
        Assert.Contains("kebab-case", problem.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Json_output_of_a_valid_folder_has_no_problems()
    {
        var (exit, output, _) = Run("", "validate", Sample, "--json");

        Assert.Equal(0, exit);
        using var document = JsonDocument.Parse(output);
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(0, document.RootElement.GetProperty("problems").GetArrayLength());
    }

    [Fact]
    public void Json_output_says_the_names_were_checked_against_the_agents_contract()
    {
        var requested = new List<Uri>();
        using var agent = TestAgent.AgentServing(TestAgent.Contract, requested);

        var (exit, output, _) = Run(agent, "", "validate", Sample, "--json");

        Assert.Equal(0, exit);
        using var document = JsonDocument.Parse(output);
        Assert.True(document.RootElement.GetProperty("namesChecked").GetBoolean());
        Assert.Equal(new Uri("http://localhost:7010/suwageval/contract"), Assert.Single(requested));
    }

    [Fact]
    public void A_tool_the_running_agent_does_not_have_is_a_problem()
    {
        var dir = Folder(TestAgent.Yaml, ("a.yaml", TestAgent.Scenario.Replace("refund_order", "refund_it", StringComparison.Ordinal)));

        var (exit, output, _) = Run("", "validate", dir);

        Assert.Equal(1, exit);
        Assert.Contains(
            "No agent has the tool 'refund_it' (the tools: get_order, refund_order, charge_card)",
            output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_endpoint_option_replaces_the_one_of_the_agent_file()
    {
        var requested = new List<Uri>();
        using var agent = TestAgent.AgentServing(TestAgent.Contract, requested);

        var (exit, _, _) = Run(agent, "", "validate", Sample, "--endpoint", "http://elsewhere.test:9000/base/");

        Assert.Equal(0, exit);
        Assert.Equal(new Uri("http://elsewhere.test:9000/base/suwageval/contract"), Assert.Single(requested));
    }

    [Fact]
    public void An_agent_that_cannot_be_reached_is_a_notice_and_the_exit_stays_0()
    {
        using var agent = TestAgent.Agent(_ => throw new HttpRequestException("connection refused"));

        var (exit, output, error) = Run(agent, "", "validate", Sample);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(
            $"names not checked: agent at http://localhost:7010 unreachable (connection refused){Environment.NewLine}valid (3 scenarios, 5 checks){Environment.NewLine}",
            output
        );
    }

    [Fact]
    public void An_unreachable_agent_is_namesChecked_false_in_json_and_does_not_hide_problems()
    {
        using var agent = TestAgent.Agent(_ => throw new HttpRequestException("connection refused"));
        var dir = Folder(TestAgent.Yaml, ("a.yaml", "key: Bad\nconversation:\n  - user: hi\n"));

        var (exit, output, _) = Run(agent, "", "validate", dir, "--json");

        Assert.Equal(1, exit);
        using var document = JsonDocument.Parse(output);
        Assert.False(document.RootElement.GetProperty("namesChecked").GetBoolean());
        Assert.Equal(1, document.RootElement.GetProperty("problems").GetArrayLength());
    }

    [Fact]
    public void An_agent_without_a_contract_endpoint_is_a_notice_too()
    {
        using var agent = TestAgent.Agent(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var (exit, output, _) = Run(agent, "", "validate", Sample);

        Assert.Equal(0, exit);
        Assert.StartsWith("names not checked: agent at http://localhost:7010 has no readable contract", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Offline_asks_the_agent_nothing_and_says_names_were_not_checked()
    {
        var requested = new List<Uri>();
        using var agent = TestAgent.AgentServing(TestAgent.Contract, requested);
        var dir = Folder(TestAgent.Yaml, ("a.yaml", TestAgent.Scenario.Replace("refund_order", "refund_it", StringComparison.Ordinal)));

        var (exit, output, _) = Run(agent, "", "validate", dir, "--offline");

        Assert.Equal(0, exit);
        Assert.Empty(requested);
        Assert.Equal($"names not checked: --offline{Environment.NewLine}valid (1 scenarios, 1 checks){Environment.NewLine}", output);
    }

    [Fact]
    public void A_version_key_in_the_agent_file_is_an_error()
    {
        var dir = Folder(TestAgent.Yaml + "\nversion:\n  systemPrompt: p\n  model: { name: m }\n");

        var (exit, output, _) = Run("", "validate", dir);

        Assert.Equal(1, exit);
        Assert.Contains(
            "version: A version is not written in a file: versions come from the agent's /suwageval/contract",
            output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_unknown_option_exits_non_zero() => Assert.NotEqual(0, Run("", "validate", "--bogus").Exit);
}
