using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;

namespace Suwageval.Server.Agents;

/// <summary>Reading what a version is made of; its content and hash are <see cref="AgentContractHash"/>'s, shared with the agents.</summary>
internal static class ContractContent
{
    /// <summary>For reading what the slices stored: unknown fields are skipped (a scenario's key and tags are stored beside its content).</summary>
    public static readonly JsonSerializerOptions Lenient = new(ContractJson.Options)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    /// <summary>Reads stored content back into a contract. Lenient about fields the contract no longer knows.</summary>
    public static AgentContract Read(string json) =>
        JsonSerializer.Deserialize<AgentContract>(json, Lenient) ?? throw new InvalidOperationException("A stored version is empty.");

    /// <summary>The distinct model names of the contract's LLM agents, in the agents' order: what a version is filtered and compared by.</summary>
    public static IReadOnlyList<string> ModelNames(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return [.. contract.Agents.Select(agent => agent.Model.Name).Distinct()];
    }
}
