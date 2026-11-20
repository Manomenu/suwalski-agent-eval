using System.Text.Json;

namespace Suwageval.Gate.Repository;

public sealed class CriteriaExamplesTests
{
    [Fact]
    public void A_source_file_that_criteria_json_reads_as_prose_is_named_whole_and_holds_no_test()
    {
        using var config = JsonDocument.Parse(GuardTests.Repository.Read("criteria.json"));
        var sources = config
            .RootElement.GetProperty("prose")
            .EnumerateArray()
            .Select(path => path.GetString() ?? "")
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(sources);
        foreach (var source in sources)
        {
            Assert.False(
                source.Contains('*', StringComparison.Ordinal),
                $"{source}: name the one file, a glob would hide guardrails from the criteria step"
            );
            var text = GuardTests.Repository.Read(source);
            Assert.False(
                text.Contains("[Fact", StringComparison.Ordinal)
                    || text.Contains("[Theory", StringComparison.Ordinal)
                    || text.Contains("[Trait", StringComparison.Ordinal),
                $"{source} holds a test, but the criteria step does not read this file: move the test out, it must count as a guardrail"
            );
        }
    }
}
