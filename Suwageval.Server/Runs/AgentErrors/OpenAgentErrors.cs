namespace Suwageval.Server.Runs.AgentErrors;

/// <summary>
/// Which agent errors are open: the rule, over rows already read. A scenario's errors are open, and listed, only while the newest
/// considered version that ran the scenario has agent errors in it: one clean run of a newer version closes them.
/// </summary>
internal static class OpenAgentErrors
{
    /// <summary>
    /// The rows of <paramref name="rows"/> (one per version and scenario, with or without errors) that are open, in their order.
    /// <paramref name="versionsNewestFirst"/> are the considered versions, newest first; rows of other versions are ignored.
    /// </summary>
    public static IReadOnlyList<AgentErrorRow> Find(IEnumerable<AgentErrorRow> rows, IReadOnlyList<Guid> versionsNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(versionsNewestFirst);
        var rank = versionsNewestFirst.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index);
        var considered = rows.Where(row => rank.ContainsKey(row.AgentVersionId)).ToList();
        var newest = considered
            .GroupBy(row => row.ScenarioKey)
            .ToDictionary(group => group.Key, group => group.Min(row => rank[row.AgentVersionId]));
        return [.. considered.Where(row => row.Errors > 0 && rank[row.AgentVersionId] == newest[row.ScenarioKey])];
    }
}
