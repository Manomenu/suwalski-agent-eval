namespace Suwageval.Server.Evaluation.Runs.AgentErrors;

internal sealed record AgentErrorRow(
    Guid AgentVersionId,
    string ScenarioKey,
    int Samples,
    int Errors,
    Guid? NewestErrorRunId,
    string? NewestError
);
