using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents;

/// <summary>What a version is made of: the contract an agent served, without its label, and the hash that tells "the same again" from "changed".</summary>
internal static class ContractContent
{
    /// <summary>The stored content. The label names the version and is no part of it, so renaming alone is not a new version.</summary>
    public static string Json(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return JsonSerializer.Serialize(contract with { Label = null }, ContractJson.Options);
    }

    public static string Hash(AgentContract contract) => ContentHash.Of(Json(contract));

    /// <summary>What is wrong with a contract an agent served, one sentence each; empty when it can be registered.</summary>
    public static IReadOnlyList<string> Problems(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(contract.SystemPrompt))
        {
            problems.Add("systemPrompt is empty");
        }
        if (string.IsNullOrWhiteSpace(contract.Model.Name))
        {
            problems.Add("model.name is empty");
        }
        var tools = new HashSet<string>();
        for (var i = 0; i < contract.Tools.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(contract.Tools[i].Name) || !tools.Add(contract.Tools[i].Name))
            {
                problems.Add($"tools[{i}].name: a tool has a name, and its name is unique");
            }
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
