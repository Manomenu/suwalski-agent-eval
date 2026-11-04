using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class RepositoryLayoutTests
{
    [Fact]
    public void Every_top_level_folder_is_described_in_agents_md()
    {
        var folders = RepositoryFiles
            .Paths()
            .Where(path => path.Contains('/', StringComparison.Ordinal))
            .Select(path => path.Split('/')[0])
            .ToHashSet();
        var described = DescribedPaths(RepositoryFiles.Read("AGENTS.md"));

        var missing = folders.Where(folder => !described.Contains(folder)).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, $"add a row to AGENTS.md, section 1 (\"What lives where\"), for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void No_two_files_differ_only_in_letter_case()
    {
        var clashes = RepositoryFiles
            .Paths()
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" vs ", group.Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(clashes.Count == 0, $"paths that differ only in letter case: {string.Join("; ", clashes)}");
    }

    [Fact]
    public void No_two_web_modules_in_one_folder_differ_only_in_letter_case_of_their_name()
    {
        var clashes = RepositoryFiles
            .Paths()
            .Where(path => path.StartsWith("Suwageval.Web/src/", StringComparison.Ordinal))
            .Where(path => path.EndsWith(".ts", StringComparison.Ordinal) || path.EndsWith(".tsx", StringComparison.Ordinal))
            .Select(path => (Folder: path[..path.LastIndexOf('/')], File: path[(path.LastIndexOf('/') + 1)..]))
            .Select(item => (item.Folder, Stem: item.File.Split('.')[0]))
            .Distinct()
            .GroupBy(item => (item.Folder, Stem: item.Stem.ToUpperInvariant()))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" vs ", group.Select(item => $"{item.Folder}/{item.Stem}").Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            clashes.Count == 0,
            $"web files whose names differ only in letter case, imports would resolve to the wrong one: {string.Join("; ", clashes)}"
        );
    }

    private static HashSet<string> DescribedPaths(string agents)
    {
        var section = agents[agents.IndexOf("## 1. What lives where", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("\n## 2.", StringComparison.Ordinal)];
        return
        [
            .. section
                .Split('\n')
                .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
                .SelectMany(line => BacktickedPath().Matches(line.Split('|')[1]))
                .Select(match => match.Groups[1].Value.Split('/')[0]),
        ];
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex BacktickedPath();
}
