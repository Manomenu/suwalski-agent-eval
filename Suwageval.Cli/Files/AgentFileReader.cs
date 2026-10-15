using System.Text.Json.Nodes;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

/// <summary>Reads <c>agent.yaml</c>: the <see cref="AgentDefinition"/> fields and, under <c>version:</c>, the <see cref="AgentVersionInput"/>.</summary>
internal static class AgentFileReader
{
    /// <summary>The agent, or null when anything in the file is a problem (they are all added to <paramref name="problems"/>).</summary>
    public static LoadedAgent? Read(string file, string text, List<Problem> problems)
    {
        var source = YamlSource.Read(file, text, problems);
        if (source is null)
        {
            return null;
        }
        if (source.Root is not JsonObject root)
        {
            problems.Add(source.Problem("", "The file is a mapping with 'name', 'endpointUrl' and 'version'."));
            return null;
        }

        var before = problems.Count;
        var definitionFields = new JsonObject();
        foreach (var (name, value) in root.Where(field => field.Key != "version"))
        {
            definitionFields[name] = value?.DeepClone();
        }
        var definition = ContractReader.Read<AgentDefinition>(source, definitionFields, "", problems);

        if (!root.TryGetPropertyValue("version", out var versionNode))
        {
            problems.Add(source.Problem("", "Missing required field: version."));
            return null;
        }
        if (versionNode is not JsonObject version)
        {
            problems.Add(source.Problem("version", "'version' is a mapping: systemPrompt, model, tools, context."));
            return null;
        }

        // The contract reports the first mistake it meets in what it reads, so the lists are read item by item first:
        // every broken tool and event is then reported, not only the first.
        var rest = (JsonObject)version.DeepClone();
        ReadItems<ToolDefinition>(source, rest, "tools", "version", problems);
        if (rest["context"] is JsonObject context)
        {
            ReadItems<EventDeclaration>(source, context, "events", "version.context", problems);
        }
        ContractReader.Read<AgentVersionInput>(source, rest, "version", problems);

        if (problems.Count != before || definition is null)
        {
            return null;
        }
        var versionInput = ContractReader.Read<AgentVersionInput>(source, version, "version", problems);
        return versionInput is null ? null : new LoadedAgent(source, definition, versionInput);
    }

    private static void ReadItems<T>(YamlSource source, JsonObject parent, string name, string parentPath, List<Problem> problems)
        where T : class
    {
        if (parent[name] is not JsonArray items)
        {
            return;
        }
        for (var i = 0; i < items.Count; i++)
        {
            ContractReader.Read<T>(source, items[i], $"{YamlSource.Child(parentPath, name)}[{i}]", problems);
        }
        parent[name] = new JsonArray();
    }
}
