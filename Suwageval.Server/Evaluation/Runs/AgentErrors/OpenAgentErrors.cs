namespace Suwageval.Server.Evaluation.Runs.AgentErrors;

internal static class OpenAgentErrors
{
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
