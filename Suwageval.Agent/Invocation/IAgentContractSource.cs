using Suwageval.Contracts.Agents;

namespace Suwageval.Contracts.Invocation;

/// <summary>Builds the contract the agent serves at <c>GET /suwageval/contract</c>: what it is configured as right now.</summary>
public interface IAgentContractSource
{
    Task<AgentContract> GetContractAsync(CancellationToken cancellationToken);
}
