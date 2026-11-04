using System.Globalization;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

internal static class VersionDeleteText
{
    public static string Preview(VersionDeletion contents) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"version {contents.VersionId}  {contents.Label ?? "-"}\nwould delete: {contents.Runs} run(s), {contents.Samples} sample(s), {contents.Verdicts} verdict(s){(contents.Conversations > 0 ? $", {contents.Conversations} monitored conversation(s)" : "")}"
        );

    public static bool Confirms(Guid versionId, string? typed) => typed == versionId.ToString();
}
