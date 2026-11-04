namespace Suwageval.Server.Evaluation.Checks;

internal sealed record CheckRow(
    Guid Id,
    Guid AgentId,
    string Key,
    string ScenarioKey,
    int Revision,
    string Content,
    string ContentHash,
    DateTime CreatedAt
);
