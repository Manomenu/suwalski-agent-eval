using System.Text.Json.Nodes;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

/// <summary>
/// Reads <c>agent.yaml</c>: the <see cref="AgentDefinition"/> fields, <c>name</c>, <c>endpointUrl</c> and the optional <c>regression</c>. What the agent
/// is (prompt, model, tools) is not in the file: the running agent serves it at <c>/suwageval/contract</c>.
/// </summary>
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

        var definition = ContractReader.Read<AgentDefinition>(source, root, "", problems);
        if (definition is null)
        {
            return null;
        }
        var regression = RegressionRules.Problems(definition.Regression);
        problems.AddRange(regression.Select(problem => source.Problem(problem.Path, problem.Message)));
        return regression.Count == 0 ? new LoadedAgent(source, definition) : null;
    }
}
