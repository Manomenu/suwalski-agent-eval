namespace Suwageval.Contracts.Agents;

public sealed record AgentDeletion(
    Guid AgentId,
    string Name,
    int Versions,
    int Scenarios,
    int Checks,
    int Runs,
    int Samples,
    int Verdicts,
    int Conversations = 0
);
