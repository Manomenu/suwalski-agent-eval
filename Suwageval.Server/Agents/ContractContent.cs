using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;

namespace Suwageval.Server.Agents;

internal static class ContractContent
{
    public static readonly JsonSerializerOptions Lenient = new(ContractJson.Options)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    public static AgentContract Read(string json) =>
        JsonSerializer.Deserialize<AgentContract>(json, Lenient) ?? throw new InvalidOperationException("A stored version is empty.");

    public static IReadOnlyList<string> ModelNames(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return [.. contract.Agents.Select(agent => agent.Model.Name).Distinct()];
    }
}
