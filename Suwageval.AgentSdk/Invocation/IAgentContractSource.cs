using Suwageval.AgentSdk.Agents;

namespace Suwageval.AgentSdk.Invocation;

public interface IAgentContractSource
{
    Task<AgentContract> GetContractAsync(CancellationToken cancellationToken);
}
