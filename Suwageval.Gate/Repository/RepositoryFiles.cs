using System.Diagnostics;

namespace Suwageval.Gate.Repository;

internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    // Why: tracked and untracked alike, so a new file is judged before its commit; deleted ones still listed by the index are not there to read
    public static string[] Paths()
    {
        var git = new ProcessStartInfo("git", "ls-files --cached --others --exclude-standard")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(git) ?? throw new InvalidOperationException("git did not start");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return
        [
            .. output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.TrimEnd('\r'))
                .Where(path => File.Exists(Path.Combine(Root, path))),
        ];
    }

    public static IEnumerable<string> CSharpSources() =>
        Paths()
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(".g.cs", StringComparison.Ordinal) && !path.EndsWith(".Designer.cs", StringComparison.Ordinal));

    public static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));

    public static string[] Lines(string path) => File.ReadAllLines(Path.Combine(Root, path));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Suwageval.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("Suwageval.slnx not found above " + AppContext.BaseDirectory);
    }
}
