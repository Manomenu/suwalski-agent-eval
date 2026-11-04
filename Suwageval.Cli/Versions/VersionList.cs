using System.Globalization;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

internal static class VersionList
{
    public static string Line(AgentVersion version, string? environment = null)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Id}  {version.Label ?? "-"}  {version.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z  {(version.ArchivedAt is null ? "active" : "archived")}  runs {version.Runs}  group runs {version.GroupRuns}"
        );
        var monitoring = VersionUsages.MonitoringIn(version, environment);
        if (monitoring.Count == 0)
        {
            return line;
        }
        var current = monitoring.Where(entry => entry.Current).Select(entry => entry.Environment).ToList();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{line}  conversations 30 d {monitoring.Sum(entry => entry.Conversations30Days)}  last conversation {monitoring.Max(entry => entry.LastConversationAt).UtcDateTime:yyyy-MM-dd HH:mm}Z{(current.Count > 0 ? $"  current ({string.Join(", ", current)})" : "")}{(version.Runs + version.GroupRuns == 0 ? "  not evaluated" : "")}"
        );
    }
}
