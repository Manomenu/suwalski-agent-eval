using Suwageval.Server.Agents;

namespace Suwageval.Server.Evaluation.Runs;

internal static class RecentRuns
{
    public const int MaxRuns = 200;

    public static List<Guid> Considered(IReadOnlyList<AgentVersionRow> versionsNewestFirst, Guid[]? requested) =>
        [
            .. versionsNewestFirst
                .Where(row => requested is { Length: > 0 } ? requested.Contains(row.Id) : row.ArchivedAt is null)
                .Select(row => row.Id),
        ];

    public static async Task<IReadOnlyList<EffectiveRuns.Point>> PickAsync(
        Guid agentId,
        IReadOnlyCollection<Guid> considered,
        IReadOnlyCollection<string> dataVersions,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var newestFirst = (await runs.ListAsync(agentId, cancellationToken)).Where(run =>
            considered.Contains(run.AgentVersionId)
            && (dataVersions.Count == 0 || (run.DataVersion is { } data && dataVersions.Contains(data)))
        );
        return EffectiveRuns.Pick([.. newestFirst], MaxRuns);
    }
}
