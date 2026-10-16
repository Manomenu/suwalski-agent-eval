using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Suwageval.Server.Tests;

/// <summary>
/// Every top-level folder of the repository has its row in AGENTS.md, section 1 ("What lives where"), so a new folder
/// cannot appear without saying what it holds and where it sits (AGENTS.md, section 4: "Keep the contracts complete").
/// A repo-content check: it reads what git tracks, not the disk, so build output and local caches do not count.
/// </summary>
public sealed partial class RepositoryLayoutTests
{
    [Fact]
    public void Every_top_level_folder_is_described_in_agents_md()
    {
        var root = RepositoryRoot();
        var folders = TrackedFiles(root)
            .Where(path => path.Contains('/', StringComparison.Ordinal))
            .Select(path => path.Split('/')[0])
            .ToHashSet();
        var described = DescribedPaths(File.ReadAllText(Path.Combine(root, "AGENTS.md")));

        var missing = folders.Where(folder => !described.Contains(folder)).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, $"add a row to AGENTS.md, section 1 (\"What lives where\"), for: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Windows and macOS file systems ignore letter case: two tracked paths equal ignoring case cannot be checked out there.
    /// </summary>
    [Fact]
    public void No_two_files_differ_only_in_letter_case()
    {
        var clashes = TrackedFiles(RepositoryRoot())
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" vs ", group.Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(clashes.Count == 0, $"paths that differ only in letter case: {string.Join("; ", clashes)}");
    }

    /// <summary>
    /// Imports in the web app omit the extension, so on a case-insensitive file system <c>./VersionDiff</c> may resolve to
    /// <c>versionDiff.ts</c>. Companions (<c>name.test.ts</c>) share the stem before the first dot, and must spell it alike. Only .ts and .tsx count: a stylesheet is imported with its extension.
    /// </summary>
    [Fact]
    public void No_two_web_modules_in_one_folder_differ_only_in_letter_case_of_their_name()
    {
        var clashes = TrackedFiles(RepositoryRoot())
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

    /// <summary>The first path segment of every backticked path in the first column of section 1's table.</summary>
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

    private static string[] TrackedFiles(string root)
    {
        var git = new ProcessStartInfo("git", "ls-files") { WorkingDirectory = root, RedirectStandardOutput = true };
        using var process = Process.Start(git) ?? throw new InvalidOperationException("git did not start");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Suwageval.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Suwageval.slnx not found above the test binaries");
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex BacktickedPath();
}
