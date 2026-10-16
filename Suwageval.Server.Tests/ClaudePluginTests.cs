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
    public void Every_skill_and_command_starts_with_a_description()
    {
        var plugin = Path.Combine(RepoRoot, "claude-plugin");
        var skills = Directory.GetFiles(plugin, "SKILL.md", SearchOption.AllDirectories);
        var commands = Directory.GetFiles(Path.Combine(plugin, "commands"), "*.md");
        Assert.NotEmpty(skills);
        Assert.NotEmpty(commands);

        foreach (var file in skills.Concat(commands))
        {
            var frontmatter = Frontmatter(file);
            Assert.True(frontmatter.TryGetValue("description", out var description) && description.Length > 0, $"{file}: no description");
        }
        foreach (var skill in skills)
        {
            Assert.Equal(new DirectoryInfo(Path.GetDirectoryName(skill)!).Name, Frontmatter(skill).GetValueOrDefault("name"));
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
