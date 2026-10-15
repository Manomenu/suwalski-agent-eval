using AgentEval.Server.Settings;

namespace AgentEval.Server.Tests.Settings;

public sealed class DotEnvTests
{
    [Fact]
    public void Lines_become_values_and_comments_are_skipped()
    {
        var text = "# a comment\n\nPORT=6220\nCORS_ORIGINS=[\"http://localhost:3220\"]\r\nNAME=\"quoted value\"\nEMPTY=\n";

        Assert.Equal(
            [new("PORT", "6220"), new("CORS_ORIGINS", "[\"http://localhost:3220\"]"), new("NAME", "quoted value"), new("EMPTY", "")],
            DotEnv.Parse(text, ".env")
        );
    }

    [Fact]
    public void A_line_without_a_key_names_its_place() =>
        Assert.Equal(
            ".env:2: expected KEY=VALUE, got \"oops\"",
            Assert.Throws<FormatException>(() => DotEnv.Parse("A=1\noops", ".env").ToList()).Message
        );

    [Fact]
    public void Later_files_win_and_missing_ones_are_skipped()
    {
        var directory = Directory.CreateTempSubdirectory("dotenv-");
        var first = Path.Combine(directory.FullName, ".env.base");
        var second = Path.Combine(directory.FullName, ".env");
        File.WriteAllText(first, "PORT=1\nHOST=a\n");
        File.WriteAllText(second, "PORT=2\n");

        var values = DotEnv.Load([first, Path.Combine(directory.FullName, "missing"), second]);

        Assert.Equal(("2", "a"), (values["PORT"], values["HOST"]));
        directory.Delete(recursive: true);
    }

    [Fact]
    public void The_files_go_from_the_solution_to_the_project()
    {
        var root = Path.GetFullPath("/repo");

        Assert.Equal(
            [
                Path.Combine(root, ".env.base"),
                Path.Combine(root, ".env"),
                Path.Combine(root, "agenteval_server", ".env.base"),
                Path.Combine(root, "agenteval_server", ".env"),
            ],
            DotEnv.Files(Path.Combine(root, "agenteval_server", "AgentEval.Server"), solutionRoot: null)
        );
    }
}
