using System.Text.Json;

namespace Suwageval.Cli.Tests.Validate;

public sealed class ValidateCommandTests : IDisposable
{
    private readonly string work = Directory.CreateTempSubdirectory("suwageval-cli-").FullName;

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    public void Dispose() => Directory.Delete(work, recursive: true);

    private static (int Exit, string Out, string Error) Run(string stdin, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(args, new StringReader(stdin), output, error);
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
    public void An_unknown_option_exits_non_zero() => Assert.NotEqual(0, Run("", "validate", "--bogus").Exit);
}
