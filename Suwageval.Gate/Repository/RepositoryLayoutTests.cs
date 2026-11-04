using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class RepositoryLayoutTests
{
    private const string Map = "docs/architecture.md";
    private const int MaxMapLineLength = 300;

    [Fact]
    public void Every_top_level_folder_is_in_the_map_of_architecture_md()
    {
        var folders = RepositoryFiles
            .Paths()
            .Where(path => path.Contains('/', StringComparison.Ordinal))
            .Select(path => path.Split('/')[0])
            .ToHashSet();
        var described = MapLines()
            .SelectMany(line => BacktickedPath().Matches(line.Split(" — ")[0]))
            .Select(match => match.Groups[1].Value.Split('/')[0])
            .ToHashSet();

        var missing = folders.Except(described).Order(StringComparer.Ordinal).ToList();
        var stale = described
            .Where(entry => !folders.Contains(entry) && !File.Exists(Path.Combine(RepositoryFiles.Root, entry)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, $"add a line to the map in {Map} (- `Folder/` — what it holds) for: {string.Join(", ", missing)}");
        Assert.True(stale.Count == 0, $"the map in {Map} names paths that do not exist: delete the lines for {string.Join(", ", stale)}");
    }

    [Fact]
    public void Every_line_of_the_map_is_short()
    {
        var tooLong = MapLines().Where(line => line.Length > MaxMapLineLength).Select(line => line[..40]).ToList();

        Assert.True(
            tooLong.Count == 0,
            $"shorten these lines of the map in {Map} to {MaxMapLineLength} characters: {string.Join("; ", tooLong)}"
        );
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

    [Fact]
    public void Every_shell_script_is_executable_in_git()
    {
        // Why: the index, not the file system: a checkout with core.fileMode=false has no exec bit to read
        var git = new ProcessStartInfo("git", "ls-files --stage -- *.sh")
        {
            WorkingDirectory = RepositoryFiles.Root,
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(git) ?? throw new InvalidOperationException("git did not start");
        var notExecutable = process
            .StandardOutput.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("100755", StringComparison.Ordinal))
            .Select(line => line.Split('\t')[1].TrimEnd('\r'))
            .ToList();
        process.WaitForExit();

        Assert.True(notExecutable.Count == 0, $"make them executable: git update-index --chmod=+x {string.Join(" ", notExecutable)}");
    }

    private static string[] MapLines() =>
        [
            .. RepositoryFiles
                .Lines(Map)
                .SkipWhile(line => line != "## The map")
                .TakeWhile(line => line != "## The gate")
                .Where(line => line.StartsWith("- `", StringComparison.Ordinal)),
        ];

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex BacktickedPath();
}
