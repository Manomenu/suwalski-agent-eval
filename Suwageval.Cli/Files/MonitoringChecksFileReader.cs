using System.Text.Json.Nodes;
using Suwageval.Contracts.Checks;

namespace Suwageval.Cli.Files;

internal static class MonitoringChecksFileReader
{
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
