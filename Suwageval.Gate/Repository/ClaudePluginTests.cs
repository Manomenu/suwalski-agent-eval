using System.Text.Json;

namespace Suwageval.Gate.Repository;

public sealed class ClaudePluginTests
{
    private static readonly string RepoRoot = RepositoryFiles.Root;

    [Fact]
    public void Every_plugin_of_the_marketplace_has_its_folder_and_manifest()
    {
        using var marketplace = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, ".claude-plugin", "marketplace.json")));
        var plugins = marketplace.RootElement.GetProperty("plugins").EnumerateArray().ToList();
        Assert.NotEmpty(plugins);

        foreach (var plugin in plugins)
        {
            var folder = Path.Combine(RepoRoot, plugin.GetProperty("source").GetString() ?? "");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, ".claude-plugin", "plugin.json")));
            Assert.Equal(plugin.GetProperty("name").GetString(), manifest.RootElement.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Every_skill_has_a_description_and_the_name_of_its_folder()
    {
        var skills = Directory.GetFiles(Path.Combine(RepoRoot, "claude-plugin", "skills"), "SKILL.md", SearchOption.AllDirectories);
        Assert.NotEmpty(skills);
        // Why: commands are skills now: a commands folder would be a second place for the same thing
        Assert.False(
            Directory.Exists(Path.Combine(RepoRoot, "claude-plugin", "commands")),
            "claude-plugin/commands/: write a skill instead"
        );

        foreach (var skill in skills)
        {
            var frontmatter = Frontmatter(skill);
            Assert.True(frontmatter.TryGetValue("description", out var description) && description.Length > 0, $"{skill}: no description");
            Assert.Equal(new FileInfo(skill).Directory?.Name, frontmatter.GetValueOrDefault("name"));
        }
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-12")]
    public void The_plugin_and_the_front_doors_tell_an_agent_how_to_write_a_guardrail_run_the_gate_and_read_its_verdict()
    {
        var reference = File.ReadAllText(Path.Combine(RepoRoot, "claude-plugin", "skills", "suwageval", "SKILL.md"));
        var run = File.ReadAllText(Path.Combine(RepoRoot, "claude-plugin", "skills", "run", "SKILL.md"));
        var frontDoors = File.ReadAllText(Path.Combine(RepoRoot, "docs", "front-doors.md"));
        var scenario = File.ReadAllText(Path.Combine(RepoRoot, "claude-plugin", "skills", "scenario", "SKILL.md"));

        Assert.Contains("suwageval gate --json", reference, StringComparison.Ordinal);
        Assert.Contains("belongs to the agent repository's local full gate", reference, StringComparison.Ordinal);
        Assert.Contains("`gate` exits 0 when every acceptance criterion passed, 1 when one failed, 2", reference, StringComparison.Ordinal);
        Assert.Contains("gate.failedGuardrail", run, StringComparison.Ordinal);
        Assert.Contains("`gate` to run every scenario with an `acGuardrail`", frontDoors, StringComparison.Ordinal);
        Assert.Contains("gets an `acGuardrail:` section with that criterion's `id`", scenario, StringComparison.Ordinal);
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
        var script = Assert.Single(command.GetProperty("args").EnumerateArray()).GetString() ?? "";
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
