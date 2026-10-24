using System.Globalization;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>
/// One version as a line of <c>suwageval versions</c>: id, label, creation time, and "archived" when it is, then its runs outside a group and
/// in one; for a version answering monitored conversations (in
/// <c>environment</c> when one is named) its conversations of 30 days, the last one, "current" where it answers now and "not evaluated"
/// when it has no run.
/// </summary>
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
