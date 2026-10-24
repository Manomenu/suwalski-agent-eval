namespace Suwageval.Contracts.Agents;

/// <summary>
/// What deleting an agent removes with it: its versions, scenarios and checks, every run with its samples and the verdicts on those, and
/// its monitored conversations.
/// </summary>
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
