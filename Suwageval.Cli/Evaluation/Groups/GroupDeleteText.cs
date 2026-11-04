using System.Globalization;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Groups;

internal static class GroupDeleteText
{
    public static string Preview(RunGroupDeletion contents) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"group {contents.Name}\nwould delete: {contents.Runs} run(s), {contents.Samples} sample(s), {contents.Verdicts} verdict(s)"
        );

    public static bool Confirms(string name, string? typed) => typed == name;
}
