using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Init;

/// <summary>
/// <c>suwageval init [path]</c>: sets up <c>suwageval/agent.yaml</c> and <c>suwageval/scenarios/</c> in an agent's repository. It never calls the
/// server, and it only changes what it is told to: the fields of an existing file that are not set stay as written, comments included, and
/// running it again with the same values writes nothing. Missing values are asked for when a person is at the terminal, else they take
/// their defaults. Exits 0, or 1 for a value that is not valid or an agent.yaml that cannot be read.
/// </summary>
internal static class InitCommand
{
    public const string DefaultEndpointUrl = "http://localhost:8000";

    private const string Name = "name";
    private const string EndpointUrl = "endpointUrl";
    private const string SuwagevalUrl = AgentFileReader.SuwagevalUrlField;

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

        var command = new Command(
            "init",
            "Set up suwageval/agent.yaml and suwageval/scenarios/ in an agent's repository. Existing files keep everything that is not set here (comments too); running it again with the same values changes nothing. Asks for missing values when run in a terminal."
        )
        {
            path,
            name,
            endpointUrl,
            suwagevalUrl,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                RunAsync(
                    parsed.GetRequiredValue(path),
                    new Given(parsed.GetValue(name), parsed.GetValue(endpointUrl), parsed.GetValue(suwagevalUrl)),
                    terminal,
                    parsed.InvocationConfiguration,
                    cancellationToken
                )
        );
        return command;
    }

    private sealed record Given(string? Name, string? EndpointUrl, string? SuwagevalUrl);

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

        var invalid = Invalid(fields);
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
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(file, AgentYamlEditor.NewFile(fields), cancellationToken);
            await output.WriteLineAsync($"{shown}: created");
            foreach (var (field, value) in fields)
            {
                await output.WriteLineAsync($"  {field}: {value}");
            }
        }
        else
        {
            var (edited, changes) =
                AgentYamlEditor.Edit(file, text, fields, []) ?? throw new InvalidOperationException("The file was read above.");
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

        await output.WriteLineAsync(
            "Next: write a scenario in the scenarios folder (see docs/scenario-format.md), then `suwageval validate` and `suwageval push`."
        );
        return 0;
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

    /// <summary>One question: the answer, or <paramref name="proposal"/> for an empty line (or none).</summary>
    private static async Task<string?> AskAsync(string field, string? proposal, Terminal terminal, TextWriter output)
    {
        await output.WriteAsync(proposal is null ? $"{field} (empty: not written): " : $"{field} [{proposal}]: ");
        var answer = (await terminal.Input.ReadLineAsync())?.Trim();
        return string.IsNullOrEmpty(answer) ? proposal : answer;
    }
}
