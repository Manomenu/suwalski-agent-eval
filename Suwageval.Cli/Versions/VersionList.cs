using System.Globalization;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>One version as a line of <c>suwageval versions</c>: id, label, creation time, and "archived" when it is, then "group runs only" for a version whose every run is in a group.</summary>
internal static class VersionList
{
    public static string Line(AgentVersion version) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Id}  {version.Label ?? "-"}  {version.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z  {(version.ArchivedAt is null ? "active" : "archived")}{(version.GroupRunsOnly ? "  group runs only" : "")}"
        );
}
