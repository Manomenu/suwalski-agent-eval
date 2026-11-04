using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using Suwageval.Cli.Files;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli;

internal static class HelpSections
{
    public static readonly IReadOnlyList<(string Title, string[] Commands)> All =
    [
        (
            "Evaluation",
            [
                "validate",
                "push",
                "run",
                "recheck",
                "gate",
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

    // Why: System.CommandLine's help builder is internal, so the root's help is written here
    public static void Apply(RootCommand root)
    {
        var option = root.Options.OfType<HelpOption>().Single();
        // Why: the root's help option always has an action, and it is a synchronous one
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

    private static AgentDefinition? LocalAgent() => AgentReader.Quietly(AgentFolder.DefaultDir)?.Definition;

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
