using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public static class AgentContractHash
{
    public static string Content(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var context = contract.Context with
        {
            Retrieval = contract.Context.Retrieval is { } retrieval ? retrieval with { DataVersion = null, Description = null } : null,
        };
        return JsonSerializer.Serialize(contract with { Label = null, Context = context }, ContractJson.Options);
    }

    public static string Of(AgentContract contract) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Content(contract))));
}
