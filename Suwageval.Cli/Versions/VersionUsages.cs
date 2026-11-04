using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

internal static class VersionUsages
{
    public static IReadOnlySet<VersionUsage> Default { get; } =
        new HashSet<VersionUsage> { VersionUsage.Evaluation, VersionUsage.Monitoring, VersionUsage.Unused };

    public static IReadOnlyList<VersionMonitoring> MonitoringIn(AgentVersion version, string? environment)
    {
        ArgumentNullException.ThrowIfNull(version);
        return [.. version.Monitoring.Where(monitoring => environment is null || monitoring.Environment == environment)];
    }

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
