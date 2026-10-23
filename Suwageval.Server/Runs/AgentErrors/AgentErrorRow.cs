namespace Suwageval.Server.Runs.AgentErrors;

/// <summary>
/// One scenario of one agent version over the finished runs read: the samples asked, those without an answer, and the newest of them (none
/// when there was no error).
/// </summary>
internal sealed record AgentErrorRow(
    Guid AgentVersionId,
    string ScenarioKey,
    int Samples,
    int Errors,
    Guid? NewestErrorRunId,
    string? NewestError
);
