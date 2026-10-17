using System.Text.Json.Nodes;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

/// <summary>
/// Reads <c>agent.yaml</c>: the <see cref="AgentDefinition"/> fields, <c>name</c>, <c>endpointUrl</c> and the optional <c>regression</c>, and
/// the CLI's own <c>suwagevalUrl</c>, which is taken out before the contract reads the rest, so the record the API shares stays as it is.
/// What the agent is (prompt, model, tools) is not in the file: the running agent serves it at <c>/suwageval/contract</c>.
/// </summary>
internal static class AgentFileReader
{
    public const string SuwagevalUrlField = "suwagevalUrl";

    /// <summary>The file's <c>suwagevalUrl</c> when it is a valid address, else null; problems are not reported (the file may be broken otherwise).</summary>
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

        var definition = ContractReader.Read<AgentDefinition>(source, root, "", problems);
        if (definition is null)
        {
            return null;
        }
        var found = AgentRules.Problems(definition);
        problems.AddRange(found.Select(problem => source.Problem(problem.Path, problem.Message)));
        return found.Count == 0 && urlValid ? new LoadedAgent(source, definition, suwagevalUrl) : null;
    }
}
