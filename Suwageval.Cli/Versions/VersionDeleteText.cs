using System.Globalization;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>What <c>suwageval versions delete</c> says and the rule that confirms it.</summary>
internal static class VersionDeleteText
{
    public static string Preview(VersionDeletion contents) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"version {contents.VersionId}  {contents.Label ?? "-"}\nwould delete: {contents.Runs} run(s), {contents.Samples} sample(s), {contents.Verdicts} verdict(s)"
        );

    /// <summary>The person confirms by typing the version's id, exactly.</summary>
    public static bool Confirms(Guid versionId, string? typed) => typed == versionId.ToString();
}
