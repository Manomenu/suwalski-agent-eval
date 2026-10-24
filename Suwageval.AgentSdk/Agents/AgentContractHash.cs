using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

/// <summary>
/// What tells one version of an agent from another: the contract without what only names or explains it, and its hash. The
/// server registers versions with these very functions, so an agent that sends the hash of its contract names the version
/// the server knows it by.
/// </summary>
public static class AgentContractHash
{
    /// <summary>
    /// The contract as the version's content: what the agent does. The label names a version for people, and the retrieval data
    /// (its version and description) is what the agent reads, kept on each run: neither is part of it, so changing only those is
    /// not a new version.
    /// </summary>
    public static string Content(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var context = contract.Context with
        {
            Retrieval = contract.Context.Retrieval is { } retrieval ? retrieval with { DataVersion = null, Description = null } : null,
        };
        return JsonSerializer.Serialize(contract with { Label = null, Context = context }, ContractJson.Options);
    }

    /// <summary>SHA-256 of <see cref="Content"/>, in lowercase hex.</summary>
    public static string Of(AgentContract contract) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Content(contract))));
}
