using System.ComponentModel;
using System.Diagnostics;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Files;

/// <summary>
/// Reads where the agent's repository stands (<see cref="RunOrigin"/>): from the CI's environment when there is one (GitHub Actions, GitLab CI),
/// otherwise by running <c>git</c> in the agent's folder. It never fails a run: no git, no repository or a git that cannot answer is no origin.
/// </summary>
/// <param name="git">Runs <c>git</c> with the arguments in a folder; null when it cannot be started.</param>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class RunOriginReader(
    Func<string, IReadOnlyList<string>, RunOriginReader.GitAnswer?> git,
    Func<string, string?> environment
)
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The real thing: the <c>git</c> on the PATH and the process's environment.</summary>
    public static RunOriginReader System { get; } = new(RunGit, Environment.GetEnvironmentVariable);

    /// <summary>What a <c>git</c> command printed and how it ended.</summary>
    internal sealed record GitAnswer(int ExitCode, string Output);

    /// <param name="dir">The agent's folder (inside the repository).</param>
    /// <param name="compareWith">The branch given with <c>--ref</c>, if any: it goes into the origin even when there is no repository.</param>
    public RunOrigin? Read(string dir, string? compareWith)
    {
        var origin = FromCi(dir) ?? FromRepository(dir);
        if (compareWith is null)
        {
            return origin;
        }
        return (origin ?? new RunOrigin { Clean = false, Pushed = false }) with { CompareWith = compareWith };
    }

    private RunOrigin? FromCi(string dir)
    {
        if (Is("GITHUB_ACTIONS"))
        {
            return new RunOrigin
            {
                Branch = Set("GITHUB_HEAD_REF") ?? Set("GITHUB_REF_NAME"),
                Commit = Set("GITHUB_SHA"),
                TargetBranch = Set("GITHUB_BASE_REF"),
                DefaultBranch = DefaultBranch(dir),
                Clean = true,
                Pushed = true,
            };
        }
        if (Is("GITLAB_CI"))
        {
            return new RunOrigin
            {
                Branch = Set("CI_MERGE_REQUEST_SOURCE_BRANCH_NAME") ?? Set("CI_COMMIT_REF_NAME"),
                Commit = Set("CI_COMMIT_SHA"),
                TargetBranch = Set("CI_MERGE_REQUEST_TARGET_BRANCH_NAME"),
                DefaultBranch = Set("CI_DEFAULT_BRANCH") ?? DefaultBranch(dir),
                Clean = true,
                Pushed = true,
            };
        }
        return null;
    }

    private RunOrigin? FromRepository(string dir)
    {
        var commit = Output(dir, "rev-parse", "HEAD");
        if (commit is null)
        {
            return null;
        }
        var branch = Output(dir, "rev-parse", "--abbrev-ref", "HEAD");
        if (branch == "HEAD")
        {
            // Detached: no branch.
            branch = null;
        }
        var clean = Output(dir, "status", "--porcelain") is "";
        var pushed = branch is not null && git(dir, ["merge-base", "--is-ancestor", "HEAD", $"origin/{branch}"]) is { ExitCode: 0 };
        return new RunOrigin
        {
            Branch = branch,
            Commit = commit,
            Clean = clean,
            Pushed = pushed,
            DefaultBranch = DefaultBranch(dir),
        };
    }

    private string? DefaultBranch(string dir)
    {
        var head = Output(dir, "symbolic-ref", "--short", "refs/remotes/origin/HEAD");
        return head is not null && head.StartsWith("origin/", StringComparison.Ordinal) ? head["origin/".Length..] : head;
    }

    /// <summary>What the command printed (trimmed) when it succeeded; null when it failed or could not run.</summary>
    private string? Output(string dir, params string[] arguments) =>
        git(dir, arguments) is { ExitCode: 0 } answer ? answer.Output.Trim() : null;

    private bool Is(string name) => string.Equals(environment(name), "true", StringComparison.OrdinalIgnoreCase);

    private string? Set(string name) => environment(name) is { Length: > 0 } value ? value : null;

    private static GitAnswer? RunGit(string dir, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }
            var output = process.StandardOutput.ReadToEndAsync();
            // Drained so a chatty git cannot block on a full pipe.
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GitTimeout))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            return new GitAnswer(process.ExitCode, output.GetAwaiter().GetResult());
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            // git is not installed or the folder does not exist: there is no origin.
            return null;
        }
    }
}
