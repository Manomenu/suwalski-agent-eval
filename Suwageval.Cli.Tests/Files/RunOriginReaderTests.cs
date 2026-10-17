using System.Diagnostics;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Tests.Files;

/// <summary>The facts a run carries about the repository: from the CI's environment, from the repository's own answers, and never an error.</summary>
public sealed class RunOriginReaderTests
{
    private static RunOriginReader Reader(
        Func<string, RunOriginReader.GitAnswer?> answer,
        Dictionary<string, string>? environment = null
    ) => new((_, args) => answer(string.Join(' ', args)), name => environment?.GetValueOrDefault(name));

    private static RunOriginReader.GitAnswer Ok(string output) => new(0, output + "\n");

    private static RunOriginReader.GitAnswer Failed => new(128, "");

    [Fact]
    public void A_clean_pushed_branch_is_read_from_the_repository()
    {
        var reader = Reader(args =>
            args switch
            {
                "rev-parse HEAD" => Ok("abc123"),
                "rev-parse --abbrev-ref HEAD" => Ok("feature-x"),
                "status --porcelain" => Ok(""),
                "symbolic-ref --short refs/remotes/origin/HEAD" => Ok("origin/master"),
                "merge-base --is-ancestor HEAD origin/feature-x" => Ok(""),
                _ => Failed,
            }
        );

        var origin = reader.Read("suwageval", null);

        Assert.NotNull(origin);
        Assert.Equal("feature-x", origin.Branch);
        Assert.Equal("abc123", origin.Commit);
        Assert.Equal("master", origin.DefaultBranch);
        Assert.True(origin.Clean);
        Assert.True(origin.Pushed);
        Assert.Null(origin.TargetBranch);
        Assert.Null(origin.CompareWith);
    }

    [Fact]
    public void Changes_in_the_tree_and_a_commit_missing_on_the_remote_are_not_clean_and_not_pushed()
    {
        var reader = Reader(args =>
            args switch
            {
                "rev-parse HEAD" => Ok("abc123"),
                "rev-parse --abbrev-ref HEAD" => Ok("feature-x"),
                "status --porcelain" => Ok(" M suwageval/agent.yaml"),
                _ => Failed,
            }
        );

        var origin = reader.Read("suwageval", null);

        Assert.NotNull(origin);
        Assert.False(origin.Clean);
        Assert.False(origin.Pushed);
        Assert.Null(origin.DefaultBranch);
    }

    [Fact]
    public void A_detached_head_has_no_branch_and_is_not_pushed()
    {
        var reader = Reader(args =>
            args switch
            {
                "rev-parse HEAD" => Ok("abc123"),
                "rev-parse --abbrev-ref HEAD" => Ok("HEAD"),
                "status --porcelain" => Ok(""),
                _ => Failed,
            }
        );

        var origin = reader.Read("suwageval", null);

        Assert.NotNull(origin);
        Assert.Null(origin.Branch);
        Assert.False(origin.Pushed);
    }

    [Fact]
    public void No_repository_or_no_git_is_no_origin()
    {
        Assert.Null(Reader(_ => Failed).Read("suwageval", null));
        Assert.Null(Reader(_ => null).Read("suwageval", null));
    }

    [Fact]
    public void Ref_names_the_branch_to_compare_with_even_without_a_repository()
    {
        var origin = Reader(_ => null).Read("suwageval", "release");

        Assert.NotNull(origin);
        Assert.Equal("release", origin.CompareWith);
        Assert.False(origin.Clean);
        Assert.False(origin.Pushed);
    }

    [Fact]
    public void GitHub_Actions_wins_over_the_repository_and_names_the_target_of_a_pull_request()
    {
        var reader = Reader(
            args => args == "symbolic-ref --short refs/remotes/origin/HEAD" ? Ok("origin/main") : Failed,
            new Dictionary<string, string>
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_HEAD_REF"] = "feature-x",
                ["GITHUB_REF_NAME"] = "12/merge",
                ["GITHUB_SHA"] = "def456",
                ["GITHUB_BASE_REF"] = "main",
            }
        );

        var origin = reader.Read("suwageval", null);

        Assert.NotNull(origin);
        Assert.Equal(("feature-x", "def456", "main", "main"), (origin.Branch, origin.Commit, origin.TargetBranch, origin.DefaultBranch));
        Assert.True(origin.Clean);
        Assert.True(origin.Pushed);
    }

    [Fact]
    public void GitHub_Actions_on_a_branch_push_uses_the_ref_name()
    {
        var reader = Reader(
            _ => null,
            new Dictionary<string, string>
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_HEAD_REF"] = "",
                ["GITHUB_REF_NAME"] = "master",
                ["GITHUB_SHA"] = "def456",
            }
        );

        var origin = reader.Read("suwageval", null);

        Assert.NotNull(origin);
        Assert.Equal("master", origin.Branch);
        Assert.Null(origin.TargetBranch);
        Assert.Null(origin.DefaultBranch);
    }

    [Fact]
    public void GitLab_CI_is_read_from_its_variables()
    {
        var reader = Reader(
            _ => null,
            new Dictionary<string, string>
            {
                ["GITLAB_CI"] = "true",
                ["CI_COMMIT_REF_NAME"] = "refs-name",
                ["CI_MERGE_REQUEST_SOURCE_BRANCH_NAME"] = "feature-y",
                ["CI_COMMIT_SHA"] = "def456",
                ["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "master",
                ["CI_DEFAULT_BRANCH"] = "master",
            }
        );

        var origin = reader.Read("suwageval", "release");

        Assert.NotNull(origin);
        Assert.Equal(
            ("feature-y", "def456", "master", "master", "release"),
            (origin.Branch, origin.Commit, origin.TargetBranch, origin.DefaultBranch, origin.CompareWith)
        );
        Assert.True(origin.Pushed);
    }

    /// <summary>The real git against a real repository with a bare remote: what the CLI reads for a clean, pushed commit, then for a changed tree and a new local commit.</summary>
    [Fact]
    public void A_real_repository_is_read_with_the_real_git()
    {
        var root = Path.Combine(Path.GetTempPath(), "suwageval-origin-" + Guid.NewGuid().ToString("N"));
        try
        {
            var remote = Path.Combine(root, "remote.git");
            var work = Path.Combine(root, "work");
            Directory.CreateDirectory(remote);
            Directory.CreateDirectory(Path.Combine(work, "suwageval"));
            Assert.SkipUnless(Run(remote, "init", "--bare", "-b", "main"), "git is not on the PATH (or is too old for -b).");
            Assert.True(Run(work, "init", "-b", "main"));
            Assert.True(Run(work, "config", "user.email", "test@example.com"));
            Assert.True(Run(work, "config", "user.name", "Test"));
            Assert.True(Run(work, "config", "commit.gpgsign", "false"));
            File.WriteAllText(Path.Combine(work, "suwageval", "agent.yaml"), "name: a\n");
            Assert.True(Run(work, "add", "."));
            Assert.True(Run(work, "commit", "-m", "first"));
            Assert.True(Run(work, "remote", "add", "origin", remote));
            Assert.True(Run(work, "push", "-u", "origin", "main"));
            Assert.True(Run(work, "remote", "set-head", "origin", "main"));
            var agentDir = Path.Combine(work, "suwageval");
            // The real git, but no CI's environment: under GitHub Actions the reader would take the CI's branch instead.
            var reader = new RunOriginReader(RunOriginReader.RunGit, _ => null);

            var pushed = reader.Read(agentDir, null);

            Assert.NotNull(pushed);
            Assert.Equal("main", pushed.Branch);
            Assert.Equal("main", pushed.DefaultBranch);
            Assert.True(pushed.Clean);
            Assert.True(pushed.Pushed);
            Assert.Equal(40, pushed.Commit?.Length);

            File.WriteAllText(Path.Combine(agentDir, "agent.yaml"), "name: b\n");
            var changed = reader.Read(agentDir, null);
            Assert.NotNull(changed);
            Assert.False(changed.Clean);
            Assert.True(changed.Pushed);

            Assert.True(Run(work, "commit", "-am", "second"));
            var local = reader.Read(agentDir, null);
            Assert.NotNull(local);
            Assert.True(local.Clean);
            Assert.False(local.Pushed);
            Assert.NotEqual(pushed.Commit, local.Commit);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static bool Run(string dir, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
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
                return false;
            }
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        // git marks its object files read-only, which Directory.Delete refuses on Windows.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }
}
