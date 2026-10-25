using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using Suwageval.Cli.Files;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli;

/// <summary>
/// The sections of <c>suwageval --help</c>: the commands by area (AGENTS.md, section 4), in the order they are listed. The names of the
/// commands stay flat; only the help groups them. Every registered command is in exactly one section (<c>HelpSectionsTests</c>), so a new
/// command cannot be forgotten.
/// </summary>
internal static class HelpSections
{
    /// <summary>Title and command names, in the order the help lists them. A new area adds its section here.</summary>
    public static readonly IReadOnlyList<(string Title, string[] Commands)> All =
    [
        (
            "Evaluation",
            [
                "validate",
                "push",
                "run",
                "recheck",
                "cancel",
                "results",
                "runs",
                "compare",
                "sweep",
                "groups",
                "note",
                "scenarios",
                "scenario",
                "trend",
                "problems",
            ]
        ),
        ("Monitoring", ["monitoring"]),
        ("Agent", ["init", "agents", "agent", "versions", "delete-agent"]),
    ];

    /// <summary>
    /// Makes the root's own help list its commands by section instead of one flat list. System.CommandLine's help builder is internal, so
    /// the root's help is written here (same layout as the default); the help of a command stays the library's.
    /// </summary>
    public static void Apply(RootCommand root)
    {
        var option = root.Options.OfType<HelpOption>().Single();
        option.Action = new RootHelpAction(root, (SynchronousCommandLineAction)option.Action!);
    }

    private sealed class RootHelpAction(RootCommand root, SynchronousCommandLineAction commandHelp) : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => commandHelp.ClearsParseErrors;

        public override int Invoke(ParseResult parseResult)
        {
            if (parseResult.CommandResult.Command != root)
            {
                return commandHelp.Invoke(parseResult);
            }
            var output = parseResult.InvocationConfiguration.Output;
            output.WriteLine("Description:");
            output.WriteLine("  " + root.Description);
            output.WriteLine();
            output.WriteLine("Usage:");
            output.WriteLine($"  {root.Name} [command] [options]");
            output.WriteLine();
            WriteSection(output, "Options", root.Options.Where(option => !option.Hidden).Select(OptionRow));
            var local = LocalAgent();
            foreach (var (title, names) in All)
            {
                WriteSection(
                    output,
                    local is not null && IsOff(local, title) ? $"{title} (off for {local.Name})" : title,
                    names.Select(name => CommandRow(root.Subcommands.Single(command => command.Name == name)))
                );
            }
            return 0;
        }
    }

    /// <summary>The agent of <c>./suwageval/agent.yaml</c>, when there is one that reads; nothing is reported (it is only the help).</summary>
    private static AgentDefinition? LocalAgent() => AgentReader.Quietly(AgentFolder.DefaultDir)?.Definition;

    /// <summary>Whether the section's area is switched off in the local file (as the server has it once pushed).</summary>
    private static bool IsOff(AgentDefinition agent, string title) =>
        title switch
        {
            "Evaluation" => agent.Features?.Evaluation == false,
            "Monitoring" => agent.Features?.Monitoring is null,
            _ => false,
        };

    private static (string Left, string Right) OptionRow(Option option) =>
        (
            string.Join(
                ", ",
                new[] { option.Name }
                    .Concat(option.Aliases)
                    .Where(name => !name.StartsWith('/'))
                    .OrderBy(name => name.StartsWith("--", StringComparison.Ordinal))
                    .ThenBy(name => name, StringComparer.Ordinal)
            ),
            option.Description ?? ""
        );

    private static (string Left, string Right) CommandRow(Command command)
    {
        var arguments = command.Arguments.Where(argument => !argument.Hidden).ToArray();
        var left = string.Join(" ", new[] { command.Name }.Concat(arguments.Select(argument => $"<{argument.Name}>")));
        var defaults = arguments.Where(argument => argument.HasDefaultValue).Select(argument => $"[default: {argument.GetDefaultValue()}]");
        return (left, string.Join(" ", new[] { command.Description ?? "" }.Concat(defaults)));
    }

    private static void WriteSection(TextWriter output, string title, IEnumerable<(string Left, string Right)> rows)
    {
        var all = rows.ToArray();
        var indent = 2;
        var width = all.Max(row => row.Left.Length) + 2;
        // Like the default help: the description wraps within the terminal's width, never when the output is redirected.
        var room = Console.IsOutputRedirected ? int.MaxValue : Math.Max(Console.WindowWidth - indent - width, 30);
        output.WriteLine(title + ":");
        foreach (var (left, right) in all)
        {
            var lines = Wrap(right, room);
            output.WriteLine($"{new string(' ', indent)}{left.PadRight(width)}{lines[0]}");
            foreach (var line in lines.Skip(1))
            {
                output.WriteLine(new string(' ', indent + width) + line);
            }
        }
        output.WriteLine();
    }

    private static List<string> Wrap(string text, int room)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > room)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = line.Length == 0 ? word : line + " " + word;
            }
        }
        lines.Add(line);
        return lines;
    }
}
