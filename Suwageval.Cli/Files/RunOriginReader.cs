using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Files;

internal sealed class RunOriginReader(
    Func<string, IReadOnlyList<string>, RunOriginReader.GitAnswer?> git,
    Func<string, string?> environment
)
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(10);

    public static RunOriginReader System { get; } = new(RunGit, Environment.GetEnvironmentVariable);

    internal sealed record GitAnswer(int ExitCode, string Output);

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

    private string? Output(string dir, params string[] arguments) =>
        git(dir, arguments) is { ExitCode: 0 } answer ? answer.Output.Trim() : null;

    private bool Is(string name) => string.Equals(environment(name), "true", StringComparison.OrdinalIgnoreCase);

    private string? Set(string name) => environment(name) is { Length: > 0 } value ? value : null;

    internal static GitAnswer? RunGit(string dir, IReadOnlyList<string> arguments)
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
            var output = new StringBuilder();
            process.OutputDataReceived += (_, line) =>
            {
                if (line.Data is not null)
                {
                    output.Append(line.Data).Append('\n');
                }
            };
            process.BeginOutputReadLine();
            // Why: drained so a chatty git cannot block on a full pipe
            process.BeginErrorReadLine();
            if (!process.WaitForExit(GitTimeout))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            // Why: without a timeout this second wait is the one that lets the last output lines arrive
            process.WaitForExit();
            return new GitAnswer(process.ExitCode, output.ToString());
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
