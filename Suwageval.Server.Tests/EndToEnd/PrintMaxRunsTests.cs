extern alias Cli;

using System.Text.RegularExpressions;
using CompareCommand = Cli::Suwageval.Cli.Evaluation.Compare.CompareCommand;

namespace Suwageval.Server.Tests.EndToEnd;

/// <summary>
/// How many runs a printed comparison holds is the web's number (<c>PRINT_MAX_RUNS</c> in <c>Suwageval.Web/src/compare/layout.ts</c>, derived
/// there from the printed column widths); <c>suwageval compare --open --print</c> refuses more before opening the page, so it keeps the same
/// number. The two are two literals, and this is what keeps them one.
/// </summary>
public sealed partial class PrintMaxRunsTests
{
    [Fact]
    public void The_CLI_and_the_web_page_print_the_same_number_of_runs()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Suwageval.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        var layout = File.ReadAllText(Path.Combine(directory.FullName, "Suwageval.Web", "src", "compare", "layout.ts"));

        var match = PrintMaxRunsConstant().Match(layout);

        Assert.True(match.Success, "layout.ts has no `export const PRINT_MAX_RUNS = <number>;`");
        Assert.Equal(CompareCommand.PrintMaxRuns, int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"export const PRINT_MAX_RUNS = (\d+);")]
    private static partial Regex PrintMaxRunsConstant();
}
