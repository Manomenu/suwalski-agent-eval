using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>A row of <c>runs</c> with the counts of its results so far.</summary>
internal sealed record RunRow(
    Guid Id,
    Guid AgentId,
    string AgentName,
    Guid AgentVersionId,
    string? Tag,
    string EndpointUrl,
    string Status,
    string? Error,
    int SamplesTotal,
    int SamplesDone,
    int ChecksPassed,
    int ChecksFailed,
    bool ContractChangedDuringRun,
    bool CancelRequested,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt
)
{
    public RunStatus State => Enum.Parse<RunStatus>(Status);
}
