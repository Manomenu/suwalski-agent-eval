using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents;

/// <summary>What a version is made of: the contract an agent served, without its label, and the hash that tells "the same again" from "changed".</summary>
internal static class ContractContent
{
    /// <summary>
    /// The stored content. The label and the description of the retrieval data name and explain a version for people and are no
    /// part of it, so changing only those is not a new version.
    /// </summary>
    public static string Json(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var context = contract.Context with
        {
            Retrieval = contract.Context.Retrieval is { } retrieval ? retrieval with { Description = null } : null,
        };
        return JsonSerializer.Serialize(contract with { Label = null, Context = context }, ContractJson.Options);
    }

    /// <summary>The version of the retrieval data the contract declares; null for an agent without retrieval.</summary>
    public static string? DataVersion(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Context.Retrieval?.DataVersion;
    }

    /// <summary>The distinct model names of the contract's LLM agents, in the agents' order: what a version is filtered and compared by.</summary>
    public static IReadOnlyList<string> ModelNames(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return [.. contract.Agents.Select(agent => agent.Model.Name).Distinct()];
    }

    public static string Hash(AgentContract contract) => ContentHash.Of(Json(contract));

    /// <summary>What is wrong with a contract an agent served, one sentence each; empty when it can be registered.</summary>
    public static IReadOnlyList<string> Problems(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var problems = new List<string>();
        if (contract.Agents.Count == 0)
        {
            problems.Add("agents: there is at least one agent");
        }
        var names = new HashSet<string>();
        for (var i = 0; i < contract.Agents.Count; i++)
        {
            var agent = contract.Agents[i];
            if (string.IsNullOrWhiteSpace(agent.Name) || !names.Add(agent.Name))
            {
                problems.Add($"agents[{i}].name: an agent has a name, and its name is unique");
            }
            if (string.IsNullOrWhiteSpace(agent.Model.Name))
            {
                problems.Add($"agents[{i}].model.name is empty");
            }
            var tools = new HashSet<string>();
            for (var t = 0; t < agent.Tools.Count; t++)
            {
                if (string.IsNullOrWhiteSpace(agent.Tools[t].Name) || !tools.Add(agent.Tools[t].Name))
                {
                    problems.Add($"agents[{i}].tools[{t}].name: a tool has a name, and its name is unique within its agent");
                }
            }
        }
        if (contract.Context.UsesRetrieval && string.IsNullOrWhiteSpace(contract.Context.Retrieval?.DataVersion))
        {
            problems.Add(
                "context.retrieval.dataVersion: an agent that uses retrieval declares the version of its data, "
                    + "or two runs over different data would look like the same version"
            );
        }
        if (!contract.Context.UsesRetrieval && contract.Context.Retrieval is not null)
        {
            problems.Add(
                "context.retrieval: the agent does not use retrieval (usesRetrieval is false), so it has no data version to declare"
            );
        }
        var events = new HashSet<string>();
        for (var i = 0; i < contract.Context.Events.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(contract.Context.Events[i].Type) || !events.Add(contract.Context.Events[i].Type))
            {
                problems.Add($"context.events[{i}].type: an event has a type, and its type is unique");
            }
        }
        return problems;
    }
}
