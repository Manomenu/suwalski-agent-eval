using System.Text.RegularExpressions;
using AgentEval.Server.Database;

namespace AgentEval.Server.Tests.Database;

/// <summary>What the committed migration scripts look like — checked here, not by code at run time.</summary>
public sealed partial class MigrationFilesTests
{
    [GeneratedRegex(@"^\d{4}_[a-z0-9]+(_[a-z0-9]+)*\.sql$")]
    private static partial Regex Name();

    private static IEnumerable<string> Scripts() =>
        Directory
            .EnumerateFiles(Path.Combine(Migrator.DefaultDirectory, "up"))
            .Select(path => Path.GetFileName(path))
            .Where(name => name != ".gitkeep");

    [Fact]
    public void Every_script_is_named_NNNN_snake_case_sql() => Assert.All(Scripts(), name => Assert.Matches(Name(), name));

    [Fact]
    public void Script_numbers_are_unique()
    {
        var duplicated = Scripts().Select(name => name[..4]).GroupBy(number => number).Where(group => group.Count() > 1);

        Assert.Empty(duplicated);
    }
}
