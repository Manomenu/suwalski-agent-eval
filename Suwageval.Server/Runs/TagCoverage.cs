using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

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
        ArgumentNullException.ThrowIfNull(scenarios);
        var ran = ranKeys.ToHashSet();
        var existing = scenarios
            .Where(scenario =>
                ran.Contains(scenario.Key)
                || (scenario.CreatedAt <= runCreatedAt && (scenario.DeletedAt is null || scenario.DeletedAt > runCreatedAt))
            )
            .ToList();
        return existing
            .SelectMany(scenario => scenario.Tags)
            .Distinct()
            .Where(tag => existing.Where(scenario => scenario.Tags.Contains(tag)).All(scenario => ran.Contains(scenario.Key)))
            .ToHashSet();
    }
}
