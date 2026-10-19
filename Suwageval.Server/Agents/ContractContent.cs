using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents;

/// <summary>What a version is made of: the contract an agent served, without its label, and the hash that tells "the same again" from "changed".</summary>
internal static class ContractContent
{
    /// <summary>For reading what the slices stored: unknown fields are skipped (a scenario's key and tags are stored beside its content).</summary>
    public static readonly JsonSerializerOptions Lenient = new(ContractJson.Options)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

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

    /// <summary>Reads stored content back into a contract. Lenient about fields the contract no longer knows.</summary>
    public static AgentContract Read(string json) =>
        JsonSerializer.Deserialize<AgentContract>(json, Lenient) ?? throw new InvalidOperationException("A stored version is empty.");

    /// <summary>The distinct model names of the contract's LLM agents, in the agents' order: what a version is filtered and compared by.</summary>
    public static IReadOnlyList<string> ModelNames(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return [.. contract.Agents.Select(agent => agent.Model.Name).Distinct()];
    }

    public static string Hash(AgentContract contract) => ContentHash.Of(Json(contract));
}
