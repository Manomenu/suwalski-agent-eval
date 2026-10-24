using Suwageval.Cli.Output;

namespace Suwageval.Cli.Tests;

public sealed class HelpSectionsTests
{
    private static readonly string[] Registered =
    [
        .. CliApp
            .Create(new Terminal(TextReader.Null, false), TextReader.Null, null, null, null, null, null)
            .Subcommands.Select(command => command.Name),
    ];

    [Fact]
    public void Every_registered_command_is_in_exactly_one_section()
    {
        var listed = HelpSections.All.SelectMany(section => section.Commands).ToArray();

        Assert.Equal(Registered.Order(), listed.Order());
    }

    [Fact]
    public void The_root_help_lists_the_commands_by_section_and_a_command_help_stays_its_own()
    {
        using var root = new StringWriter();
        using var run = new StringWriter();
        CliApp.Run(["--help"], TextReader.Null, root, TextWriter.Null, interrupt: TestContext.Current.CancellationToken);
        CliApp.Run(["run", "--help"], TextReader.Null, run, TextWriter.Null, interrupt: TestContext.Current.CancellationToken);

        var lines = root.ToString().Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var titles = HelpSections.All.Select(section => section.Title + ":").ToArray();
        Assert.Equal(
            titles,
            lines.Where(line => line.EndsWith(':') && !line.StartsWith(' ') && line is not ("Description:" or "Usage:" or "Options:"))
        );
        Assert.DoesNotContain("Commands:", lines);
        foreach (var (title, commands) in HelpSections.All)
        {
            var section = lines.SkipWhile(line => line != title + ":").Skip(1).TakeWhile(line => line.Length > 0).ToArray();
            Assert.Equal(commands, section.Select(line => line.TrimStart().Split(' ')[0]));
        }
        Assert.Contains("Usage:", run.ToString());
        Assert.Contains("--wait", run.ToString());
    }
}
