using System.Text.Json;

namespace Suwageval.Server.Tests;

/// <summary>
/// The Claude Code plugin (<c>.claude-plugin/marketplace.json</c> and <c>claude-plugin/</c>) is only text, so nothing else in the gate
/// would notice a broken manifest or a skill without its description: Claude Code would just skip it. A repo-content test.
/// </summary>
public sealed class ClaudePluginTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
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

    [Fact]
    public void Every_plugin_of_the_marketplace_has_its_folder_and_manifest()
    {
        using var marketplace = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, ".claude-plugin", "marketplace.json")));
        var plugins = marketplace.RootElement.GetProperty("plugins").EnumerateArray().ToList();
        Assert.NotEmpty(plugins);

        foreach (var plugin in plugins)
        {
            var folder = Path.Combine(RepoRoot, plugin.GetProperty("source").GetString()!);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, ".claude-plugin", "plugin.json")));
            Assert.Equal(plugin.GetProperty("name").GetString(), manifest.RootElement.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Every_skill_has_a_description_and_the_name_of_its_folder()
    {
        var skills = Directory.GetFiles(Path.Combine(RepoRoot, "claude-plugin", "skills"), "SKILL.md", SearchOption.AllDirectories);
        Assert.NotEmpty(skills);
        // Commands are skills now (Claude Code merged them): a commands/ folder would be a second place for the same thing.
        Assert.False(
            Directory.Exists(Path.Combine(RepoRoot, "claude-plugin", "commands")),
            "claude-plugin/commands/: write a skill instead"
        );

        foreach (var skill in skills)
        {
            var frontmatter = Frontmatter(skill);
            Assert.True(frontmatter.TryGetValue("description", out var description) && description.Length > 0, $"{skill}: no description");
            Assert.Equal(new DirectoryInfo(Path.GetDirectoryName(skill)!).Name, frontmatter.GetValueOrDefault("name"));
        }
    }

    [Fact]
    public void The_session_hook_runs_a_script_of_the_plugin_whose_context_file_is_there()
    {
        var plugin = Path.Combine(RepoRoot, "claude-plugin");
        using var hooks = JsonDocument.Parse(File.ReadAllText(Path.Combine(plugin, "hooks", "hooks.json")));
        var command = Assert.Single(
            hooks
                .RootElement.GetProperty("hooks")
                .GetProperty("SessionStart")
                .EnumerateArray()
                .SelectMany(entry => entry.GetProperty("hooks").EnumerateArray())
        );
        var script = Assert.Single(command.GetProperty("args").EnumerateArray()).GetString()!;
        Assert.StartsWith("${CLAUDE_PLUGIN_ROOT}/", script, StringComparison.Ordinal);
        var path = Path.Combine(plugin, script["${CLAUDE_PLUGIN_ROOT}/".Length..]);
        Assert.True(File.Exists(path), $"{path}: missing");
        foreach (
            var context in System
                .Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), @"hooks/context/[\w.-]+\.md")
                .Select(match => match.Value)
        )
        {
            Assert.True(File.Exists(Path.Combine(plugin, context)), $"{context}: missing");
        }
    }

    /// <summary>The top-level <c>key: value</c> lines between the opening and closing <c>---</c>.</summary>
    private static Dictionary<string, string> Frontmatter(string file)
    {
        var lines = File.ReadAllLines(file);
        Assert.True(lines.Length > 0 && lines[0] == "---", $"{file}: must start with ---");
        var end = Array.IndexOf(lines, "---", 1);
        Assert.True(end > 0, $"{file}: frontmatter is not closed");
        return lines[1..end]
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }
}
