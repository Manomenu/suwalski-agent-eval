namespace Suwageval.Server.Evaluation.Checks;

/// <summary>A check with its latest revision and the key of its scenario.</summary>
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
