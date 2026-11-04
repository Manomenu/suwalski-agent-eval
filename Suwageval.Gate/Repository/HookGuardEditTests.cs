using System.Diagnostics;
using System.Text.Json;

namespace Suwageval.Gate.Repository;

public sealed class HookGuardEditTests
{
    private static (int ExitCode, string Errors) Run(string path, bool allowGateEdit = false)
    {
        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepositoryFiles.Root,
        };
        start.ArgumentList.Add("Suwageval.Gate/scripts/hook-guard-edit.sh");
        start.Environment.Remove("ALLOW_GATE_EDIT");
        if (allowGateEdit)
        {
            start.Environment["ALLOW_GATE_EDIT"] = "1";
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("bash did not start");
        process.StandardInput.Write(
            JsonSerializer.Serialize(new { tool_name = "Edit", tool_input = new { file_path = $"{RepositoryFiles.Root}/{path}" } })
        );
        process.StandardInput.Close();
        var errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, errors);
    }

    [Theory]
    [InlineData("Suwageval.Gate/scripts/check.sh")]
    [InlineData("Suwageval.Gate/scripts/hook-guard-edit.sh")]
    [InlineData("Suwageval.Gate/Design/ServerContractsTests.cs")]
    [InlineData("Suwageval.Gate/Suwageval.Gate.csproj")]
    [InlineData(".claude/settings.json")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData(".editorconfig")]
    [InlineData("Suwageval.Web/eslint.config.js")]
    [InlineData("Suwageval.Web/tsconfig.app.json")]
    [InlineData("Directory.Build.props")]
    public void An_agent_cannot_edit_the_gate_and_is_told_to_ask_the_owner(string path)
    {
        var (exitCode, errors) = Run(path);

        Assert.Equal(2, exitCode);
        Assert.Contains("ALLOW_GATE_EDIT=1 claude", errors, StringComparison.Ordinal);
        Assert.Contains("ask the user", errors, StringComparison.Ordinal);
    }

    [Fact]
    public void The_owner_lets_a_session_edit_the_gate() =>
        Assert.Equal(0, Run("Suwageval.Gate/scripts/check.sh", allowGateEdit: true).ExitCode);

    [Theory]
    [InlineData("Suwageval.Web/src/api/openapi.d.ts", "api-types.sh")]
    [InlineData("Suwageval.Server/packages.lock.json", "dotnet restore")]
    [InlineData("Suwageval.Web/pnpm-lock.yaml", "pnpm install")]
    public void Generated_files_are_refused_with_the_command_that_regenerates_them(string path, string command)
    {
        var (exitCode, errors) = Run(path);

        Assert.Equal(2, exitCode);
        Assert.Contains(command, errors, StringComparison.Ordinal);
    }

    [Fact]
    public void A_migration_that_exists_is_refused_and_a_new_one_is_not()
    {
        var existing = RepositoryFiles.Paths().First(path => path.Contains("/Database/Migrations/up/", StringComparison.Ordinal));

        Assert.Equal(2, Run(existing).ExitCode);
        Assert.Equal(0, Run("Suwageval.Server/Database/Migrations/up/9999_not_written_yet.sql").ExitCode);
    }

    [Theory]
    [InlineData("Suwageval.Server/Agents/AgentStore.cs")]
    [InlineData("Suwageval.Web/src/format/format.test.ts")]
    [InlineData("Suwageval.Gate/layers.json")]
    [InlineData("AGENTS.md")]
    public void Everything_else_stays_editable(string path) => Assert.Equal(0, Run(path).ExitCode);
}
