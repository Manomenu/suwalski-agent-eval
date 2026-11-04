using System.Diagnostics;

namespace Suwageval.Gate.Repository;

public sealed class ScriptsTests
{
    private static readonly string[] NotForCalling = ["checkout-env.sh", "hook-guard-edit.sh", "hook-stop.sh"];

    // Why: its --help goes to the pinned vdoc.py, so the word never stands in the script itself
    private const string AnswersThroughItsTool = "criteria.sh";

    public static TheoryData<string> Scripts()
    {
        var data = new TheoryData<string>();
        foreach (
            var path in RepositoryFiles
                .Paths()
                .Where(path =>
                    (
                        path.StartsWith("scripts/.internal/", StringComparison.Ordinal)
                        || path.StartsWith("Suwageval.Gate/scripts/", StringComparison.Ordinal)
                    ) && path.EndsWith(".sh", StringComparison.Ordinal)
                )
                .Where(path => !NotForCalling.Contains(Path.GetFileName(path)))
        )
        {
            data.Add(path);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Scripts))]
    public async Task Every_script_an_agent_calls_answers_help(string script)
    {
        var text = RepositoryFiles.Read(script);
        Assert.True(
            Path.GetFileName(script) == AnswersThroughItsTool
                || text.Contains("--help", StringComparison.Ordinal)
                || text.Contains("usage:", StringComparison.Ordinal),
            $"{script} would run for real on --help: AGENTS.md sends agents to --help instead of describing the commands; print what it does and how to call it on --help"
        );

        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepositoryFiles.Root,
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--help");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("bash did not start");
        var cancellation = TestContext.Current.CancellationToken;
        var error = process.StandardError.ReadToEndAsync(cancellation);
        var output = await process.StandardOutput.ReadToEndAsync(cancellation);
        var errorText = await error;
        await process.WaitForExitAsync(cancellation);

        Assert.True(output.Length + errorText.Length > 0, $"{script} --help printed nothing: print what it does and how to call it");
    }
}
