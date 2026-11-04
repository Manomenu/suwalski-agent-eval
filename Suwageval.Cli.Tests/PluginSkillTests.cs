using Suwageval.Cli.Output;

namespace Suwageval.Cli.Tests;

public sealed class PluginSkillTests
{
    [Fact]
    public void The_skill_mentions_every_command()
    {
        var skill = File.ReadAllText(Path.Combine(RepoRoot(), "claude-plugin", "skills", "suwageval", "SKILL.md"));

        var missing = Commands().Where(command => !skill.Contains($"suwageval {command}", StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, $"add to claude-plugin/skills/suwageval/SKILL.md: {string.Join(", ", missing)}");
    }

    private static List<string> Commands() =>
        [
            .. CliApp
                .Create(new Terminal(TextReader.Null, false), TextReader.Null, null, null, null, null, null)
                .Subcommands.Select(c => c.Name),
        ];

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Suwageval.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("Suwageval.slnx not found above " + AppContext.BaseDirectory);
    }
}
