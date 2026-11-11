using System.Text.Json;

namespace Suwageval.Gate.Repository;

// Why: a kind nobody explains is a kind nobody uses, and the lightest check that can fail for a criterion is then never picked
public sealed class CriteriaKindsTests
{
    [Theory]
    [InlineData("docs/development.md")]
    public void Every_kind_of_guardrail_of_criteria_json_is_explained_where_people_and_agents_read(string document)
    {
        using var config = JsonDocument.Parse(RepositoryFiles.Read("Suwageval.Gate/criteria.json"));
        var kinds = config.RootElement.GetProperty("kinds").EnumerateArray().Select(kind => kind.GetProperty("kind").GetString()).ToList();
        var text = RepositoryFiles.Read(document);

        Assert.NotEmpty(kinds);
        var missing = kinds.Where(kind => !text.Contains($"| `{kind}` |", StringComparison.Ordinal)).ToList();
        Assert.True(
            missing.Count == 0,
            $"{document} has no row for these kinds of guardrail of criteria.json: add a row `| `<kind>` | where | how it names the id |` to its table: {string.Join(", ", missing)}"
        );
    }
}
