using System.Text.Json.Nodes;
using Suwageval.Contracts.Checks;

namespace Suwageval.Cli.Files;

/// <summary>
/// Reads <c>suwageval/monitoring-checks.yaml</c>: <c>checks:</c>, a list of checks as in a scenario file, with
/// no scenario (they run on every monitored conversation of the agent). The rules of such a check are <c>MonitoringCheckRules</c>, applied
/// by the validator.
/// </summary>
internal static class MonitoringChecksFileReader
{
    /// <summary>The checks, or null when the file cannot be read as one (the problems are added to <paramref name="problems"/>).</summary>
    public static (YamlSource Source, List<CheckInput> Checks)? Read(string file, string text, List<Problem> problems)
    {
        var source = YamlSource.Read(file, text, problems);
        if (source is null)
        {
            return null;
        }
        if (source.Root is not JsonObject root || root.Count != 1 || root["checks"] is not JsonArray items)
        {
            problems.Add(source.Problem("", "The file is a mapping with one key, 'checks': a list of checks."));
            return null;
        }
        var checks = new List<CheckInput>();
        var readable = true;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not JsonObject item)
            {
                problems.Add(source.Problem($"checks[{i}]", "A check is a mapping with 'key' and 'type'."));
                readable = false;
                continue;
            }
            if (ContractReader.Read<CheckInput>(source, item, $"checks[{i}]", problems) is { } check)
            {
                checks.Add(check);
            }
            else
            {
                readable = false;
            }
        }
        return readable ? (source, checks) : null;
    }
}
