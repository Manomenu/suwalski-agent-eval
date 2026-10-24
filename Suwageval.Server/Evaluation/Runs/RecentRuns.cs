using Suwageval.Server.Agents;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>
/// The runs and versions the open problems of an agent (its unstable checks and its agent errors) are decided over: the considered
/// versions, and the newest <see cref="MaxRuns"/> finished runs of them, each with the verdicts it shows (<see cref="EffectiveRuns"/>).
/// </summary>
internal static class RecentRuns
{
    /// <summary>The unstable checks and the agent errors look at the newest this many finished runs of the agent.</summary>
    public const int MaxRuns = 200;

    /// <summary>The versions the open problems are decided over, newest first: the requested ones, else every one not archived.</summary>
    public static List<Guid> Considered(IReadOnlyList<AgentVersionRow> versionsNewestFirst, Guid[]? requested) =>
        [
            .. versionsNewestFirst
                .Where(row => requested is { Length: > 0 } ? requested.Contains(row.Id) : row.ArchivedAt is null)
                .Select(row => row.Id),
        ];

    public static async Task<IReadOnlyList<EffectiveRuns.Point>> PickAsync(
        Guid agentId,
        IReadOnlyCollection<Guid> considered,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var newestFirst = (await runs.ListAsync(agentId, cancellationToken)).Where(run => considered.Contains(run.AgentVersionId));
        return EffectiveRuns.Pick([.. newestFirst], MaxRuns);
    }
}
