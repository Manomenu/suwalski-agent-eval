using System.Text.Json.Nodes;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Files;

/// <summary>Reads one scenario file: the <see cref="ScenarioInput"/> fields plus <c>checks:</c>, whose items lack <c>scenarioKey</c>.</summary>
internal static class ScenarioFileReader
{
    /// <summary>The scenario with its checks, or null when anything in the file is a problem (all are added to <paramref name="problems"/>).</summary>
    public static LoadedScenario? Read(string file, string text, List<Problem> problems)
    {
        var source = YamlSource.Read(file, text, problems);
        if (source is null)
        {
            return null;
        }
        if (source.Root is not JsonObject root)
        {
            problems.Add(source.Problem("", "The file is a mapping with 'key', 'conversation' and 'checks'."));
            return null;
        }

        var before = problems.Count;
        var header = new JsonObject();
        foreach (var (name, value) in root.Where(field => field.Key is not ("conversation" or "checks")))
        {
            header[name] = value?.DeepClone();
        }

        var items = new JsonArray();
        if (root["conversation"] is JsonArray conversation)
        {
            for (var i = 0; i < conversation.Count; i++)
            {
                var path = $"conversation[{i}]";
                var expanded = ConversationShorthands.Expand(conversation[i], path, source, problems);
                if (expanded is not null && ContractReader.Read<ConversationItem>(source, expanded, path, problems) is not null)
                {
                    items.Add(expanded);
                }
            }
            header["conversation"] = items.DeepClone();
        }
        else if (root.ContainsKey("conversation"))
        {
            header["conversation"] = root["conversation"]?.DeepClone();
        }
        ContractReader.Read<ScenarioInput>(source, header, "", problems);

        var checks = ReadChecks(source, root, header["key"] is JsonValue key && key.TryGetValue<string>(out var k) ? k : "", problems);

        if (problems.Count != before)
        {
            return null;
        }
        var scenario = ContractReader.Read<ScenarioInput>(source, header, "", problems);
        return scenario is null ? null : new LoadedScenario(source, scenario, checks);
    }

    private static List<CheckInput> ReadChecks(YamlSource source, JsonObject root, string scenarioKey, List<Problem> problems)
    {
        var result = new List<CheckInput>();
        if (!root.TryGetPropertyValue("checks", out var node) || node is null)
        {
            return result;
        }
        if (node is not JsonArray checks)
        {
            problems.Add(source.Problem("checks", "'checks' is a list."));
            return result;
        }
        for (var i = 0; i < checks.Count; i++)
        {
            var path = $"checks[{i}]";
            if (checks[i] is not JsonObject check)
            {
                problems.Add(source.Problem(path, "A check is a mapping with 'key' and 'type'."));
                continue;
            }
            if (check.ContainsKey("scenarioKey"))
            {
                problems.Add(
                    source.Problem($"{path}.scenarioKey", "Unknown field 'scenarioKey': it is the 'key' of this file's scenario.")
                );
                continue;
            }
            var withScenario = (JsonObject)check.DeepClone();
            withScenario["scenarioKey"] = scenarioKey;
            var read = ContractReader.Read<CheckInput>(source, withScenario, path, problems);
            if (read is not null)
            {
                result.Add(read);
            }
        }
        return result;
    }
}
