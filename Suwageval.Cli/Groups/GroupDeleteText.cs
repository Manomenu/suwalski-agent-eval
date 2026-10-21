using System.Globalization;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Groups;

/// <summary>What <c>suwageval groups delete</c> says and the rule that confirms it.</summary>
internal static class GroupDeleteText
{
    public static string Preview(RunGroupDeletion contents) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"group {contents.Name}\nwould delete: {contents.Runs} run(s), {contents.Samples} sample(s), {contents.Verdicts} verdict(s)"
        );

    /// <summary>The person confirms by typing the group's name, exactly.</summary>
    public static bool Confirms(string name, string? typed) => typed == name;
}
