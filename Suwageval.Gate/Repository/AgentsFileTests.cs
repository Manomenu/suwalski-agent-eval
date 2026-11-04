using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class AgentsFileTests
{
    public const int MaxLines = 100;

    private static readonly string[] Acronyms =
    [
        "AGENTS",
        "CLAUDE",
        "FAIL",
        "HTTP",
        "JSON",
        "KISS",
        "PASS",
        "README",
        "SKIP",
        "SQL",
        "TODO",
        "YAGNI",
        "YAML",
    ];

    [GeneratedRegex(@"\b[A-Z]{4,}\b")]
    private static partial Regex Shouting();

    [Fact]
    public void AGENTS_md_stays_short_enough_to_be_read_every_session()
    {
        var lines = RepositoryFiles.Read("AGENTS.md").Split('\n').Length - 1;

        Assert.True(
            lines <= MaxLines,
            $"AGENTS.md has {lines} lines, the limit is {MaxLines}: move detail to docs/, or make the rule a type, analyzer, test or hook instead."
        );
    }

    [Fact]
    public void AGENTS_md_has_no_directory_tree()
    {
        var content = RepositoryFiles.Read("AGENTS.md");

        Assert.False(
            content.IndexOfAny(['├', '└', '│']) >= 0,
            "AGENTS.md must not draw a directory tree: it goes stale. Put the map in docs/architecture.md (RepositoryLayoutTests reads it)."
        );
    }

    [Fact]
    public void AGENTS_md_does_not_shout()
    {
        var shouted = Shouting()
            .Matches(RepositoryFiles.Read("AGENTS.md"))
            .Select(match => match.Value)
            .Where(word => !Acronyms.Contains(word))
            .Distinct()
            .Order()
            .ToList();

        Assert.True(
            shouted.Count == 0,
            $"write it in plain case and give the reason instead of capitals, or add a real acronym to Acronyms in AgentsFileTests.cs: {string.Join(", ", shouted)}"
        );
    }

    [Fact]
    public void CLAUDE_md_only_points_to_AGENTS_md()
    {
        var content = RepositoryFiles.Read("CLAUDE.md").Trim();

        Assert.True(content == "@AGENTS.md", "CLAUDE.md must contain exactly `@AGENTS.md`: edit AGENTS.md, never CLAUDE.md.");
    }
}
