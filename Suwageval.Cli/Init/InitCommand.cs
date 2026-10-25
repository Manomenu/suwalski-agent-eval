using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Init;

/// <summary>
/// <c>suwageval init [path]</c>: sets up <c>suwageval/agent.yaml</c> and <c>suwageval/scenarios/</c> in an agent's repository. It never calls the
/// server, and it only changes what it is told to: the fields of an existing file that are not set stay as written, comments included, and
/// running it again with the same values writes nothing. Missing values are asked for when a person is at the terminal, else they take
/// their defaults. Monitoring (<c>features.monitoring</c>) is added when asked for (<c>--monitoring</c>, or "y" at the terminal) and the file has
/// none yet, with an empty <c>monitoring-checks.yaml</c> and what the agent itself needs said at the end; settings already in the file are
/// changed only by hand. Exits 0, or 1 for a value that is not valid or an agent.yaml that cannot be read.
/// </summary>
internal static class InitCommand
{
    public const string DefaultEndpointUrl = "http://localhost:8000";

    private const string Name = "name";
    private const string EndpointUrl = "endpointUrl";
    private const string SuwagevalUrl = AgentFileReader.SuwagevalUrlField;

    /// <summary>The <c>closeAfter</c> proposed for new monitoring: the server's default, written out so it can be found and changed.</summary>
    public const string DefaultCloseAfter = "30m";

    private const string MonitoringChecks = """
        # The checks run on every closed conversation of the deployed agent; each one as a scenario's check (no samples, no passThreshold).
        # Format: https://github.com/Manomenu/suwalski-agent-eval/blob/master/docs/scenario-format.md#monitoring-checks-monitoring-checksyaml
        # For example:
        #   - key: polite
        #     description: Answers politely, even to a rude user.
        #     type: judge
        #     criteria: The agent stays polite and on topic.
        checks: []

        """;

    public static Command Create(Terminal terminal)
    {
        var path = new Argument<string>("path")
        {
            Description = "The agent's repository; suwageval/ is created in it.",
            DefaultValueFactory = _ => ".",
        };
        var name = new Option<string?>("--name")
        {
            Description = "The agent's name. Default: the one in agent.yaml, else the folder's name.",
        };
        var endpointUrl = new Option<string?>("--endpoint-url")
        {
            Description = $"Where the agent is reached. Default: the one in agent.yaml, else {DefaultEndpointUrl}.",
        };
        var suwagevalUrl = new Option<string?>("--suwageval-url")
        {
            Description =
                $"Suwageval's address, written as suwagevalUrl (the web app; the API is under /api). Default: the one in agent.yaml, else none: ${ServerAddress.Variable} or {ServerAddress.Default} applies.",
        };
        var monitoring = new Option<bool>("--monitoring")
        {
            Description =
                "Switch monitoring on: the deployed agent's conversations are stored and checked (features.monitoring, and an empty monitoring-checks.yaml). Asked at the terminal when agent.yaml has none.",
        };
        var closeAfter = new Option<string?>("--close-after")
        {
            Description =
                $"With monitoring: how long a conversation waits for a turn before it is closed and checked, 1m to 24h. Default: {DefaultCloseAfter}.",
        };
        var environments = new Option<string[]>("--environment")
        {
            Description = "With monitoring: an environment whose conversations are taken (repeatable). Default: none, any is taken.",
            AllowMultipleArgumentsPerToken = false,
        };

        var command = new Command(
            "init",
            "Set up suwageval/agent.yaml and suwageval/scenarios/ in an agent's repository. Existing files keep everything that is not set here (comments too); running it again with the same values changes nothing. Asks for missing values when run in a terminal."
        )
        {
            path,
            name,
            endpointUrl,
            suwagevalUrl,
            monitoring,
            closeAfter,
            environments,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                RunAsync(
                    parsed.GetRequiredValue(path),
                    new Given(
                        parsed.GetValue(name),
                        parsed.GetValue(endpointUrl),
                        parsed.GetValue(suwagevalUrl),
                        parsed.GetValue(monitoring),
                        parsed.GetValue(closeAfter),
                        parsed.GetValue(environments) is { Length: > 0 } named ? named : null
                    ),
                    terminal,
                    parsed.InvocationConfiguration,
                    cancellationToken
                )
        );
        return command;
    }

    /// <summary>What the options say; null: not given. Monitoring is asked for by <c>--monitoring</c>, <c>--close-after</c> or <c>--environment</c>.</summary>
    private sealed record Given(
        string? Name,
        string? EndpointUrl,
        string? SuwagevalUrl,
        bool Monitoring,
        string? CloseAfter,
        IReadOnlyList<string>? Environments
    );

    /// <summary>The monitoring to add: its <c>closeAfter</c> and environments (none: any).</summary>
    private sealed record NewMonitoring(string CloseAfter, IReadOnlyList<string> Environments);

    private static async Task<int> RunAsync(
        string root,
        Given given,
        Terminal terminal,
        InvocationConfiguration console,
        CancellationToken cancellationToken
    )
    {
        var dir = Path.Combine(root, AgentFolder.DefaultDir);
        var file = Path.Combine(dir, "agent.yaml");
        var scenarios = Path.Combine(dir, "scenarios");
        var text = File.Exists(file) ? await File.ReadAllTextAsync(file, cancellationToken) : null;

        var present = new Dictionary<string, string>();
        if (text is not null)
        {
            var problems = new List<Problem>();
            if (AgentYamlEditor.Current(file, text, problems) is not { } read)
            {
                foreach (var problem in problems)
                {
                    await console.Error.WriteLineAsync($"{problem.File}: {problem.Message}");
                }
                return 1;
            }
            present = read;
        }

        var fields = new List<(string Field, string Value)>();
        var folderName = Path.GetFileName(Path.GetFullPath(root));
        foreach (
            var (field, option, fallback) in new (string Field, string? Option, string? Fallback)[]
            {
                (Name, given.Name, folderName),
                (EndpointUrl, given.EndpointUrl, DefaultEndpointUrl),
                (SuwagevalUrl, given.SuwagevalUrl, null),
            }
        )
        {
            var current = present.GetValueOrDefault(field);
            var value = option;
            if (value is null)
            {
                var proposal = current ?? fallback;
                value = terminal.Interactive ? await AskAsync(field, proposal, terminal, console.Output) : proposal;
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields.Add((field, value.Trim()));
            }
        }

        var hasMonitoring = text is not null && AgentYamlEditor.HasMonitoring(file, text) is true;
        var asked = given.Monitoring || given.CloseAfter is not null || given.Environments is not null;
        NewMonitoring? newMonitoring = null;
        if (!hasMonitoring && (asked || (terminal.Interactive && await AskYesAsync(MonitoringQuestion, terminal, console.Output))))
        {
            var close =
                given.CloseAfter
                ?? (terminal.Interactive ? await AskAsync("  closeAfter", DefaultCloseAfter, terminal, console.Output) : null)
                ?? DefaultCloseAfter;
            var named =
                given.Environments
                ?? (
                    terminal.Interactive
                        ? (await AskAsync("  environments, comma-separated (empty: any)", null, terminal, console.Output) ?? "").Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                        )
                        : []
                );
            newMonitoring = new NewMonitoring(close.Trim(), named);
        }

        var invalid = Invalid(fields);
        if (newMonitoring is not null)
        {
            invalid.AddRange(
                FeatureRules
                    .Problems(
                        new AgentFeatures
                        {
                            Monitoring = new MonitoringSettings
                            {
                                CloseAfter = newMonitoring.CloseAfter,
                                Environments = newMonitoring.Environments.Count == 0 ? null : newMonitoring.Environments,
                            },
                        }
                    )
                    .Select(problem => $"{problem.Path}: {problem.Message}")
            );
        }
        if (invalid.Count > 0)
        {
            foreach (var message in invalid)
            {
                await console.Error.WriteLineAsync($"{message} Nothing was written.");
            }
            return 1;
        }

        var output = console.Output;
        var shown = file;
        if (text is null)
        {
            var created = AgentYamlEditor.NewFile(fields);
            if (newMonitoring is not null)
            {
                created =
                    AgentYamlEditor.WithMonitoring(created, newMonitoring.CloseAfter, newMonitoring.Environments)
                    ?? throw new InvalidOperationException("A new file has no features yet.");
            }
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(file, created, cancellationToken);
            await output.WriteLineAsync($"{shown}: created");
            foreach (var (field, value) in fields)
            {
                await output.WriteLineAsync($"  {field}: {value}");
            }
            if (newMonitoring is not null)
            {
                await output.WriteLineAsync($"  features.monitoring: {MonitoringText(newMonitoring)}");
            }
        }
        else
        {
            var (edited, changes) =
                AgentYamlEditor.Edit(file, text, fields, []) ?? throw new InvalidOperationException("The file was read above.");
            if (newMonitoring is not null)
            {
                if (
                    AgentYamlEditor.WithMonitoring(edited, newMonitoring.CloseAfter, newMonitoring.Environments) is not { } withMonitoring
                    || AgentYamlEditor.HasMonitoring(file, withMonitoring) is not true
                )
                {
                    await console.Error.WriteLineAsync(
                        $"{shown}: its features are written in a way init does not edit; add features.monitoring by hand (docs/scenario-format.md). Nothing was written."
                    );
                    return 1;
                }
                edited = withMonitoring;
                changes = [.. changes, new FieldChange("features.monitoring", null, MonitoringText(newMonitoring))];
            }
            if (changes.Count == 0)
            {
                await output.WriteLineAsync($"{shown}: unchanged");
            }
            else
            {
                await File.WriteAllTextAsync(file, edited, cancellationToken);
                await output.WriteLineAsync($"{shown}: updated");
                foreach (var change in changes)
                {
                    await output.WriteLineAsync($"  {change.Field}: {change.Old ?? "(not set)"} -> {change.New}");
                }
            }
        }

        var scenariosShown = scenarios + Path.DirectorySeparatorChar;
        await output.WriteLineAsync($"{scenariosShown}: {(Directory.Exists(scenarios) ? "unchanged" : "created")}");
        Directory.CreateDirectory(scenarios);

        if (newMonitoring is not null)
        {
            var checks = Path.Combine(dir, AgentFolder.MonitoringChecksFile);
            var checksExist = File.Exists(checks);
            if (!checksExist)
            {
                await File.WriteAllTextAsync(checks, MonitoringChecks, cancellationToken);
            }
            await output.WriteLineAsync($"{checks}: {(checksExist ? "unchanged" : "created")}");
        }
        else if (hasMonitoring && asked)
        {
            await output.WriteLineAsync($"{shown}: features.monitoring is there already; change its settings in the file.");
        }

        await output.WriteLineAsync(
            "Next: write a scenario in the scenarios folder (see docs/scenario-format.md), then `suwageval validate` and `suwageval push`."
        );
        if (newMonitoring is not null)
        {
            await output.WriteLineAsync(MonitoringNext(fields, newMonitoring));
        }
        return 0;
    }

    private const string MonitoringQuestion = "Monitor the deployed agent's conversations? (stored and checked by Suwageval)";

    private static string MonitoringText(NewMonitoring monitoring) =>
        $"closeAfter {monitoring.CloseAfter}, "
        + (monitoring.Environments.Count == 0 ? "any environment" : $"environments {string.Join(", ", monitoring.Environments)}");

    /// <summary>What the agent itself needs for monitoring, to copy: the SDK's two calls and its three settings.</summary>
    private static string MonitoringNext(List<(string Field, string Value)> fields, NewMonitoring monitoring)
    {
        var name = fields.First(field => field.Field == Name).Value;
        var url = fields.FirstOrDefault(field => field.Field == SuwagevalUrl).Value ?? "<Suwageval's address>";
        var environment = monitoring.Environments.Count == 0 ? "production" : monitoring.Environments[0];
        return $"""
            Monitoring: write checks in monitoring-checks.yaml, `suwageval push`, then in the agent (.NET, Suwageval.AgentSdk):
              builder.Services.AddSuwagevalMonitoring(builder.Configuration);
              // after each turn, with IConversationRecorder recorder:
              recorder.Record(conversationId, items, dataVersion); // items: the user's message and what the agent produced
            and in its environment (.env):
              Suwageval__Url={url}
              Suwageval__Agent={name}
              Suwageval__Environment={environment}
            """;
    }

    private static List<string> Invalid(List<(string Field, string Value)> fields)
    {
        var messages = new List<string>();
        foreach (var (field, value) in fields)
        {
            var message = field == Name ? null : HttpAddress.Problem(field, value);
            if (message is not null)
            {
                messages.Add($"'{value}': {message}");
            }
        }
        if (!fields.Any(f => f.Field == Name))
        {
            messages.Add("The agent needs a name.");
        }
        return messages;
    }

    /// <summary>A yes/no question, no by default: true only for an answer starting with y.</summary>
    private static async Task<bool> AskYesAsync(string question, Terminal terminal, TextWriter output)
    {
        await output.WriteAsync($"{question} [y/N]: ");
        var answer = (await terminal.Input.ReadLineAsync())?.Trim();
        return answer is { Length: > 0 } && char.ToLowerInvariant(answer[0]) == 'y';
    }

    /// <summary>One question: the answer, or <paramref name="proposal"/> for an empty line (or none).</summary>
    private static async Task<string?> AskAsync(string field, string? proposal, Terminal terminal, TextWriter output)
    {
        await output.WriteAsync(proposal is null ? $"{field} (empty: not written): " : $"{field} [{proposal}]: ");
        var answer = (await terminal.Input.ReadLineAsync())?.Trim();
        return string.IsNullOrEmpty(answer) ? proposal : answer;
    }
}
