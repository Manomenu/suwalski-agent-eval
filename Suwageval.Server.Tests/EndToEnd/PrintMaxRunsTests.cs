extern alias Cli;

using System.Text.RegularExpressions;
using Suwageval.Server.Evaluation.Runs.Compare;
using CompareCommand = Cli::Suwageval.Cli.Evaluation.Compare.CompareCommand;

namespace Suwageval.Server.Tests.EndToEnd;

// Why: the comparison limit is a literal in three languages and this test is what keeps the server, CLI and web values equal
public sealed partial class PrintMaxRunsTests
{
    [Fact]
    [Trait("criterion", "AC-product-simplification-6")]
    public void The_server_the_CLI_and_the_web_page_hold_the_same_number_of_runs_in_a_comparison()
    {
        Assert.Equal(7, RunComparisonReport.MaxRuns);
        Assert.Equal(RunComparisonReport.MaxRuns, CompareCommand.MaxRuns);
        Assert.Equal(RunComparisonReport.MaxRuns, WebConstant("src/evaluation/compare/layout.ts", PrintMaxRunsConstant()));
        Assert.Equal(RunComparisonReport.MaxRuns, WebConstant("src/routes/links.ts", CompareMaxRunsConstant()));
    }

    private static int WebConstant(string path, Regex constant)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Suwageval.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine([directory.FullName, "Suwageval.Web", .. path.Split('/')]));

        var match = constant.Match(source);

        Assert.True(match.Success, $"{path} has no `{constant}`");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"export const PRINT_MAX_RUNS = (\d+);")]
    private static partial Regex PrintMaxRunsConstant();

    [GeneratedRegex(@"export const COMPARE_MAX_RUNS = (\d+);")]
    private static partial Regex CompareMaxRunsConstant();
}
