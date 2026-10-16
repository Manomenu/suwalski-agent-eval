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
