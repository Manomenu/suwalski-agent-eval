using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class CheckoutEnvTests
{
    private const string TwoCheckouts = """
        set -euo pipefail
        unset CI TEST_CONNECTION_STRING E2E_DATABASE E2E_SERVER_PORT E2E_WEB_PORT
        cd "$1"
        git init -q main
        cd main
        git -c user.name=test -c user.email=test@example.com commit -q --allow-empty -m first
        for name in "${@:3}"; do
            git worktree add -q --detach "../$name"
            mkdir -p "../$name/scripts/.internal"
            cp "$2" "../$name/scripts/.internal/"
            (
                source "../$name/scripts/.internal/checkout-env.sh" 2>/dev/null
                echo "$TEST_CONNECTION_STRING"
                echo "$E2E_DATABASE"
            )
        done
        """;

    [Theory]
    [InlineData("suwalski-agent-eval-gate", "suwalski-agent-eval-gate-verify")]
    [InlineData("a-b", "a_b")]
    [InlineData(
        "Suwalski-Agent-Eval-With-A-Name-Far-Longer-Than-Any-Database-Allows-One",
        "Suwalski-Agent-Eval-With-A-Name-Far-Longer-Than-Any-Database-Allows-Two"
    )]
    public async Task Two_worktrees_get_their_own_valid_databases(string first, string second)
    {
        var work = Directory.CreateTempSubdirectory("suwageval-checkout-");
        try
        {
            var script = Path.Combine(RepositoryFiles.Root, "scripts", ".internal", "checkout-env.sh");
            var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-c", TwoCheckouts, "bash", work.FullName, script, first, second })
            {
                start.ArgumentList.Add(argument);
            }
            using var bash = Process.Start(start) ?? throw new InvalidOperationException("bash did not start");
            var errors = bash.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            var output = await bash.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await bash.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(bash.ExitCode == 0, await errors);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var databases = lines
                .Select(line => DatabaseOfConnection().Match(line) is { Success: true } match ? match.Groups[1].Value : line)
                .ToList();

            Assert.Equal(4, databases.Count);
            Assert.Equal(4, databases.Distinct().Count());
            Assert.All(databases, database => Assert.Matches(ValidDatabaseName(), database));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [GeneratedRegex("Database=([^;]+);")]
    private static partial Regex DatabaseOfConnection();

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex ValidDatabaseName();
}
