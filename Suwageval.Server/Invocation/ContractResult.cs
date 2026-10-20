using Suwageval.AgentSdk.Agents;

namespace Suwageval.Server.Invocation;

/// <summary>What asking the agent for its contract came to: the contract, or why there is none (the message names the URL).</summary>
internal sealed record ContractResult
{
    public AgentContract? Contract { get; private init; }

    public string? Error { get; private init; }

    public static ContractResult Succeeded(AgentContract contract) => new() { Contract = contract };

    public static ContractResult Failed(string error) => new() { Error = error };
}
