using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>Where a version is used: the kinds of usage <c>suwageval versions --usage</c> filters by (the web's Versions page has the same four).</summary>
internal enum VersionUsage
{
    /// <summary>It has runs in no group.</summary>
    Evaluation,

    /// <summary>It has runs in a group.</summary>
    Groups,

    /// <summary>It has no run at all.</summary>
    Unused,

    /// <summary>It is archived.</summary>
    Archived,
}

/// <summary>The one rule of the usage filter, written once for the CLI (and once, with the same words, for the web in <c>versionUsage.ts</c>).</summary>
internal static class VersionUsages
{
    /// <summary>What is listed when no usage is asked for: versions with runs outside a group and versions with no runs yet (a fresh push), as before the filter.</summary>
    public static IReadOnlySet<VersionUsage> Default { get; } = new HashSet<VersionUsage> { VersionUsage.Evaluation, VersionUsage.Unused };

    /// <summary>
    /// Whether the version is listed under the chosen usages: an archived version only when <see cref="VersionUsage.Archived"/> is chosen,
    /// any other when it has ANY of the chosen usages (evaluation: <c>Runs &gt; 0</c>, groups: <c>GroupRuns &gt; 0</c>, unused: no run at all).
    /// </summary>
    public static bool Matches(AgentVersion version, IReadOnlySet<VersionUsage> chosen)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(chosen);
        if (version.ArchivedAt is not null)
        {
            return chosen.Contains(VersionUsage.Archived);
        }
        return (chosen.Contains(VersionUsage.Evaluation) && version.Runs > 0)
            || (chosen.Contains(VersionUsage.Groups) && version.GroupRuns > 0)
            || (chosen.Contains(VersionUsage.Unused) && version.Runs == 0 && version.GroupRuns == 0);
    }
}
