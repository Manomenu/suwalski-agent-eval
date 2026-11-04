using System.Text.Json.Nodes;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

internal static class AgentFileReader
{
    public const string SuwagevalUrlField = "suwagevalUrl";
    public const string SweepField = "sweep";

    public static string? SuwagevalUrl(string file, string text)
    {
        var source = YamlSource.Read(file, text, []);
        return
            source?.Root is JsonObject root
            && root[SuwagevalUrlField] is JsonValue value
            && value.TryGetValue<string>(out var url)
            && HttpAddress.Problem(SuwagevalUrlField, url) is null
            ? url
            : null;
    }

    public static LoadedAgent? Read(string file, string text, List<Problem> problems)
    {
        var source = YamlSource.Read(file, text, problems);
        if (source is null)
        {
            return null;
        }
        if (source.Root is not JsonObject root)
        {
            problems.Add(source.Problem("", "The file is a mapping with 'name' and 'endpointUrl'."));
            return null;
        }
        if (root.ContainsKey("version"))
        {
            problems.Add(
                source.Problem(
                    "version",
                    "A version is not written in a file: versions come from the agent's /suwageval/contract. Remove 'version:'."
                )
            );
            return null;
        }

        string? suwagevalUrl = null;
        var urlValid = true;
        if (root.Remove(SuwagevalUrlField, out var written))
        {
            suwagevalUrl = written is JsonValue value && value.TryGetValue<string>(out var url) ? url : null;
            if (HttpAddress.Problem(SuwagevalUrlField, suwagevalUrl) is { } message)
            {
                problems.Add(source.Problem(SuwagevalUrlField, message));
                urlValid = false;
            }
        }

        SweepSettings? sweep = null;
        var sweepValid = true;
        if (root.Remove(SweepField, out var section))
        {
            sweep = ContractReader.Read<SweepSettings>(source, section, SweepField, problems);
            sweepValid = sweep is not null;
            foreach (var (path, message) in sweep?.Problems() ?? [])
            {
                problems.Add(source.Problem($"{SweepField}.{path}", message));
                sweepValid = false;
            }
        }

        var definition = ContractReader.Read<AgentDefinition>(source, root, "", problems);
        if (definition is null)
        {
            return null;
        }
        var found = AgentRules.Problems(definition);
        problems.AddRange(found.Select(problem => source.Problem(problem.Path, problem.Message)));
        return found.Count == 0 && urlValid && sweepValid ? new LoadedAgent(source, definition, suwagevalUrl, sweep) : null;
    }
}
