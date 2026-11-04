using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Evaluation.Runs;

internal static class TagCoverage
{
    public static IReadOnlySet<string> CompleteTags(
        DateTime runCreatedAt,
        IReadOnlyCollection<string> ranKeys,
        IReadOnlyList<ScenarioLifetime> scenarios
    )
    {
        ArgumentNullException.ThrowIfNull(ranKeys);
        var existing = Existing(runCreatedAt, ranKeys, scenarios);
        var ran = ranKeys.ToHashSet();
        return existing
            .SelectMany(scenario => scenario.Tags)
            .Distinct()
            .Where(tag => existing.Where(scenario => scenario.Tags.Contains(tag)).All(scenario => ran.Contains(scenario.Key)))
            .ToHashSet();
    }

    public static (int Ran, int Total) Counts(
        string tag,
        DateTime runCreatedAt,
        IReadOnlyCollection<string> ranKeys,
        IReadOnlyList<ScenarioLifetime> scenarios
    )
    {
        ArgumentNullException.ThrowIfNull(ranKeys);
        var ofTag = Existing(runCreatedAt, ranKeys, scenarios).Where(scenario => scenario.Tags.Contains(tag)).ToList();
        var ran = ranKeys.ToHashSet();
        return (ofTag.Count(scenario => ran.Contains(scenario.Key)), ofTag.Count);
    }

    private static List<ScenarioLifetime> Existing(
        DateTime runCreatedAt,
        IReadOnlyCollection<string> ranKeys,
        IReadOnlyList<ScenarioLifetime> scenarios
    )
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var ran = ranKeys.ToHashSet();
        return
        [
            .. scenarios.Where(scenario =>
                ran.Contains(scenario.Key)
                || (scenario.CreatedAt <= runCreatedAt && (scenario.DeletedAt is null || scenario.DeletedAt > runCreatedAt))
            ),
        ];
    }
}
