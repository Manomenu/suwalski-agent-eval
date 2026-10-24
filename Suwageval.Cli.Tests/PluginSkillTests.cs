using Suwageval.Cli.Output;

namespace Suwageval.Cli.Tests;

/// <summary>
/// The Claude Code plugin's skill (<c>claude-plugin/skills/suwageval/SKILL.md</c>) is how Claude learns the CLI: a command it
/// does not mention is one Claude will not use (AGENTS.md, section 5: anything the CLI can do, the plugin knows).
/// </summary>
public sealed class PluginSkillTests
{
    [Fact]
    public void The_skill_mentions_every_command()
    {
        var skill = File.ReadAllText(Path.Combine(RepoRoot(), "claude-plugin", "skills", "suwageval", "SKILL.md"));

        var missing = Commands().Where(command => !skill.Contains($"suwageval {command}", StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, $"add to claude-plugin/skills/suwageval/SKILL.md: {string.Join(", ", missing)}");
    }

    /// <summary>The top-level commands.</summary>
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
