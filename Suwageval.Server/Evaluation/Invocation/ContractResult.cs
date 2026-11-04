using Suwageval.AgentSdk.Agents;

namespace Suwageval.Server.Evaluation.Invocation;

internal sealed record ContractResult
{
    public AgentContract? Contract { get; private init; }

    public string? Error { get; private init; }

    public static ContractResult Succeeded(AgentContract contract) => new() { Contract = contract };

    public static ContractResult Failed(string error) => new() { Error = error };
}
