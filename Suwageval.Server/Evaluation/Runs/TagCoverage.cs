using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>Which tags a run covered whole: only for those does the run say anything about the tag's pass rate.</summary>
internal static class TagCoverage
{
    /// <summary>
    /// The tags whose every scenario is among <paramref name="ranKeys"/>. A tag's scenarios are the ones that existed when the run was
    /// queued (created before, not yet deleted) and the ones it ran; tags are the scenarios' current ones, they keep no history.
    /// </summary>
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

    /// <summary>How many of the scenarios the tag had when the run was queued the run ran, and how many the tag had (the same scenarios as <see cref="CompleteTags"/> weighs).</summary>
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
