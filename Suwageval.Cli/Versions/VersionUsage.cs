using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>Where a version is used: the kinds of usage <c>suwageval versions --usage</c> filters by (the web's Versions page has the same five).</summary>
internal enum VersionUsage
{
    /// <summary>It has runs in no group.</summary>
    Evaluation,

    /// <summary>It has runs in a group.</summary>
    Groups,

    /// <summary>It answered conversations of the deployed agent (monitoring).</summary>
    Monitoring,

    /// <summary>It has no run and answered no conversation.</summary>
    Unused,

    /// <summary>It is archived.</summary>
    Archived,
}

/// <summary>The one rule of the usage filter, written once for the CLI (and once, with the same words, for the web in <c>versionUsage.ts</c>).</summary>
internal static class VersionUsages
{
    /// <summary>What is listed when no usage is asked for: versions with runs outside a group, versions answering the deployed agent's conversations and versions not used yet (a fresh push).</summary>
    public static IReadOnlySet<VersionUsage> Default { get; } =
        new HashSet<VersionUsage> { VersionUsage.Evaluation, VersionUsage.Monitoring, VersionUsage.Unused };

    /// <summary>The version's monitoring in <paramref name="environment"/>, or in every environment when none is named.</summary>
    public static IReadOnlyList<VersionMonitoring> MonitoringIn(AgentVersion version, string? environment)
    {
        ArgumentNullException.ThrowIfNull(version);
        return [.. version.Monitoring.Where(monitoring => environment is null || monitoring.Environment == environment)];
    }

    /// <summary>
    /// Whether the version is listed under the chosen usages: an archived version only when <see cref="VersionUsage.Archived"/> is chosen,
    /// any other when it has ANY of the chosen usages (evaluation: <c>Runs &gt; 0</c>, groups: <c>GroupRuns &gt; 0</c>, monitoring: it answered
    /// a conversation, in <paramref name="environment"/> when one is named; unused: no run and no conversation anywhere).
    /// </summary>
    public static bool Matches(AgentVersion version, IReadOnlySet<VersionUsage> chosen, string? environment = null)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(chosen);
        if (version.ArchivedAt is not null)
        {
            return chosen.Contains(VersionUsage.Archived);
        }
        return (chosen.Contains(VersionUsage.Evaluation) && version.Runs > 0)
            || (chosen.Contains(VersionUsage.Groups) && version.GroupRuns > 0)
            || (chosen.Contains(VersionUsage.Monitoring) && MonitoringIn(version, environment).Count > 0)
            || (chosen.Contains(VersionUsage.Unused) && version.Runs == 0 && version.GroupRuns == 0 && version.Monitoring.Count == 0);
    }
}
