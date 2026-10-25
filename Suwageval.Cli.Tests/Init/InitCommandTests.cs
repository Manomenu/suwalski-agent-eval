namespace Suwageval.Cli.Tests.Init;

/// <summary>The command on a real temporary folder; it never calls a server.</summary>
public sealed class InitCommandTests : IDisposable
{
    private readonly string repo = Directory.CreateTempSubdirectory("suwageval-init-").FullName;

    private string AgentYaml => Path.Combine(repo, "suwageval", "agent.yaml");

    public void Dispose() => Directory.Delete(repo, recursive: true);

    private (int Exit, string Out, string Error) Run(string stdin = "", bool interactive = false, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["init", repo, .. args], new StringReader(stdin), output, error, interactive: interactive);
        return (exit, output.ToString(), error.ToString());
    }

    private static readonly string[] AllOptions =
    [
        "--name",
        "x",
        "--endpoint-url",
        "http://localhost:8000",
        "--suwageval-url",
        "http://localhost:3230",
    ];

    [Fact]
    public void A_new_file_gets_the_header_the_fields_and_a_scenarios_folder()
    {
        var (exit, output, _) = Run(args: AllOptions);

        Assert.Equal(0, exit);
        var text = File.ReadAllText(AgentYaml);
        Assert.StartsWith("# The agent under test.", text, StringComparison.Ordinal);
        Assert.Contains("docs/scenario-format.md", text, StringComparison.Ordinal);
        Assert.Contains(
            "name: x\nendpointUrl: http://localhost:8000\nsuwagevalUrl: http://localhost:3230\n",
            text,
            StringComparison.Ordinal
        );
        Assert.True(Directory.Exists(Path.Combine(repo, "suwageval", "scenarios")));
        Assert.Contains("agent.yaml: created", output, StringComparison.Ordinal);
        Assert.Contains("suwageval validate", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_file_it_writes_is_valid_for_validate()
    {
        Run(args: AllOptions);

        using var output = new StringWriter();
        var exit = CliApp.Run(
            ["validate", Path.Combine(repo, "suwageval"), "--offline"],
            TextReader.Null,
            output,
            TextWriter.Null,
            interrupt: TestContext.Current.CancellationToken
        );

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Without_a_terminal_the_missing_values_take_their_defaults()
    {
        var (exit, _, _) = Run();

        Assert.Equal(0, exit);
        var text = File.ReadAllText(AgentYaml);
        Assert.Contains($"name: {Path.GetFileName(repo)}\n", text, StringComparison.Ordinal);
        Assert.Contains("endpointUrl: http://localhost:8000\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("suwagevalUrl", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_run_with_the_same_values_changes_nothing()
    {
        Run(args: AllOptions);
        var before = File.GetLastWriteTimeUtc(AgentYaml);
        var text = File.ReadAllText(AgentYaml);

        var (exit, output, _) = Run(args: AllOptions);

        Assert.Equal(0, exit);
        Assert.Equal(text, File.ReadAllText(AgentYaml));
        Assert.Equal(before, File.GetLastWriteTimeUtc(AgentYaml));
        Assert.Contains("agent.yaml: unchanged", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_update_keeps_comments_formatting_and_the_other_fields()
    {
        Directory.CreateDirectory(Path.Combine(repo, "suwageval"));
        File.WriteAllText(
            AgentYaml,
            "# my note\nname:   shop   # the shop\nsamples: 5\n\nregression:\n  history: 7 # keep\nendpointUrl: \"http://old:1\"\n"
        );

        var (exit, output, _) = Run(args: ["--endpoint-url", "http://new:2", "--suwageval-url", "http://localhost:3230"]);

        Assert.Equal(0, exit);
        Assert.Equal(
            "# my note\nname:   shop   # the shop\nsamples: 5\n\nregression:\n  history: 7 # keep\nendpointUrl: http://new:2\nsuwagevalUrl: http://localhost:3230\n",
            File.ReadAllText(AgentYaml).Replace("\r\n", "\n", StringComparison.Ordinal)
        );
        Assert.Contains("endpointUrl: http://old:1 -> http://new:2", output, StringComparison.Ordinal);
        Assert.Contains("suwagevalUrl: (not set) -> http://localhost:3230", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_existing_value_not_given_stays_even_without_a_terminal()
    {
        Directory.CreateDirectory(Path.Combine(repo, "suwageval"));
        File.WriteAllText(AgentYaml, "name: shop\nendpointUrl: http://shop:9\n");

        var (exit, output, _) = Run();

        Assert.Equal(0, exit);
        Assert.Equal("name: shop\nendpointUrl: http://shop:9\n", File.ReadAllText(AgentYaml));
        Assert.Contains("unchanged", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_option_overrides_the_existing_value()
    {
        Directory.CreateDirectory(Path.Combine(repo, "suwageval"));
        File.WriteAllText(AgentYaml, "name: shop\nendpointUrl: http://shop:9\n");

        Run(args: ["--name", "store"]);

        Assert.Equal("name: store\nendpointUrl: http://shop:9\n", File.ReadAllText(AgentYaml));
    }

    [Fact]
    public void An_invalid_address_is_refused_and_nothing_is_written()
    {
        var (exit, _, error) = Run(args: ["--suwageval-url", "localhost:3230"]);

        Assert.Equal(1, exit);
        Assert.Contains("absolute http or https address", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(repo, "suwageval")));
    }

    [Fact]
    public void An_agent_yaml_that_is_not_yaml_is_refused_and_left_as_it_is()
    {
        Directory.CreateDirectory(Path.Combine(repo, "suwageval"));
        File.WriteAllText(AgentYaml, "name: [unclosed\n");

        var (exit, _, error) = Run(args: AllOptions);

        Assert.Equal(1, exit);
        Assert.NotEmpty(error);
        Assert.Equal("name: [unclosed\n", File.ReadAllText(AgentYaml));
    }

    [Fact]
    public void With_a_terminal_it_asks_for_the_missing_values_and_an_empty_answer_takes_the_default()
    {
        var (exit, output, _) = Run("my-agent\n\nhttp://suwageval.example\n", interactive: true);

        Assert.Equal(0, exit);
        Assert.Contains("name [", output, StringComparison.Ordinal);
        Assert.Contains("endpointUrl [http://localhost:8000]: ", output, StringComparison.Ordinal);
        Assert.Equal(
            "name: my-agent\nendpointUrl: http://localhost:8000\nsuwagevalUrl: http://suwageval.example\n",
            File.ReadAllText(AgentYaml).Split('\n', 4)[3].Replace("\r\n", "\n", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void With_a_terminal_it_does_not_ask_for_what_an_option_gave()
    {
        var (_, output, _) = Run("\n\n", interactive: true, "--name", "x", "--endpoint-url", "http://localhost:8000");

        Assert.DoesNotContain("name [", output, StringComparison.Ordinal);
        Assert.DoesNotContain("endpointUrl [", output, StringComparison.Ordinal);
        Assert.Contains("suwagevalUrl (empty: not written): ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_that_yaml_would_read_as_something_else_is_quoted()
    {
        Run(args: ["--name", "123", "--endpoint-url", "http://localhost:8000"]);

        Assert.Contains("name: \"123\"\n", File.ReadAllText(AgentYaml), StringComparison.Ordinal);
    }

    private bool Validates()
    {
        using var output = new StringWriter();
        return CliApp.Run(
                ["validate", Path.Combine(repo, "suwageval"), "--offline"],
                TextReader.Null,
                output,
                TextWriter.Null,
                interrupt: TestContext.Current.CancellationToken
            ) == 0;
    }

    [Fact]
    public void Monitoring_asked_by_options_is_written_with_an_empty_checks_file_and_what_the_agent_needs()
    {
        var (exit, output, _) = Run(args: [.. AllOptions, "--monitoring", "--environment", "local", "--environment", "production"]);

        Assert.Equal(0, exit);
        Assert.EndsWith(
            "features:\n  monitoring:\n    closeAfter: 30m\n    environments: [local, production]\n",
            File.ReadAllText(AgentYaml),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "checks: []",
            File.ReadAllText(Path.Combine(repo, "suwageval", "monitoring-checks.yaml")),
            StringComparison.Ordinal
        );
        Assert.Contains("builder.Services.AddSuwagevalMonitoring(builder.Configuration);", output, StringComparison.Ordinal);
        Assert.Contains(
            "Suwageval__Url=http://localhost:3230\n",
            output.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal
        );
        Assert.Contains("Suwageval__Agent=x", output, StringComparison.Ordinal);
        Assert.Contains("Suwageval__Environment=local", output, StringComparison.Ordinal);
        Assert.True(Validates());
    }

    [Fact]
    public void With_a_terminal_it_asks_about_monitoring_and_enter_takes_the_defaults()
    {
        var (exit, output, _) = Run("my-agent\n\n\ny\n\n\n", interactive: true);

        Assert.Equal(0, exit);
        Assert.Contains(
            "Monitor the deployed agent's conversations? (stored and checked by Suwageval) [y/N]: ",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains("  closeAfter [30m]: ", output, StringComparison.Ordinal);
        Assert.EndsWith("features:\n  monitoring:\n    closeAfter: 30m\n", File.ReadAllText(AgentYaml), StringComparison.Ordinal);
        Assert.True(Validates());
    }

    [Fact]
    public void With_a_terminal_no_answer_to_monitoring_leaves_it_off()
    {
        Run("my-agent\n\n\n\n", interactive: true);

        Assert.DoesNotContain("features", File.ReadAllText(AgentYaml), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo, "suwageval", "monitoring-checks.yaml")));
    }

    [Fact]
    public void Monitoring_goes_under_the_features_already_there_and_the_rest_stays_as_written()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AgentYaml)!);
        File.WriteAllText(
            AgentYaml,
            "name: x # mine\nendpointUrl: http://localhost:8000\nfeatures: # areas\n  evaluation: true\nsamples: 2\n"
        );

        var (exit, _, _) = Run(args: ["--monitoring", "--close-after", "1h"]);

        Assert.Equal(0, exit);
        Assert.Equal(
            "name: x # mine\nendpointUrl: http://localhost:8000\nfeatures: # areas\n  monitoring:\n    closeAfter: 1h\n  evaluation: true\nsamples: 2\n",
            File.ReadAllText(AgentYaml)
        );
    }

    [Fact]
    public void Monitoring_already_there_is_left_for_a_person_to_change()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AgentYaml)!);
        var text = "name: x\nendpointUrl: http://localhost:8000\nfeatures:\n  monitoring:\n    closeAfter: 5m\n";
        File.WriteAllText(AgentYaml, text);

        var (exit, output, _) = Run(args: ["--monitoring", "--close-after", "1h"]);

        Assert.Equal(0, exit);
        Assert.Equal(text, File.ReadAllText(AgentYaml));
        Assert.Contains("features.monitoring is there already", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wrong_monitoring_setting_or_features_written_inline_write_nothing()
    {
        var (wrong, _, wrongError) = Run(args: [.. AllOptions, "--close-after", "3 days"]);
        Directory.CreateDirectory(Path.GetDirectoryName(AgentYaml)!);
        File.WriteAllText(AgentYaml, "name: x\nendpointUrl: http://localhost:8000\nfeatures: { evaluation: true }\n");
        var (inline, _, inlineError) = Run(args: ["--monitoring"]);

        Assert.Equal((1, 1), (wrong, inline));
        Assert.Contains("features.monitoring.closeAfter: closeAfter is a number with s, m or h", wrongError, StringComparison.Ordinal);
        Assert.Contains("add features.monitoring by hand", inlineError, StringComparison.Ordinal);
        Assert.Equal("name: x\nendpointUrl: http://localhost:8000\nfeatures: { evaluation: true }\n", File.ReadAllText(AgentYaml));
    }
}
