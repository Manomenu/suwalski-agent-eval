using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>A row of <c>runs</c> with the counts of its results so far.</summary>
internal sealed record RunRow(
    Guid Id,
    Guid AgentId,
    string AgentName,
    Guid AgentVersionId,
    string TagsJson,
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
    DateTime? FinishedAt,
    Guid? RecheckOf = null,
    string SkippedScenarios = "[]",
    Guid? VerdictsFrom = null,
    string? Origin = null,
    bool Reference = false,
    string? ComparedWith = null,
    string JudgeModels = "[]",
    int SamplesFailed = 0,
    string? RunGroup = null,
    string? Label = null,
    string? Note = null,
    string? DataVersion = null,
    string? DataDescription = null,
    int? GateRetryLimit = null
)
{
    /// <summary>A gate run (<c>suwageval gate</c>): <see cref="GateRetryLimit"/> is the agent's default number of attempts when it started.</summary>
    public bool Gate => GateRetryLimit is not null;

    /// <summary>What a re-check of this run inherits.</summary>
    public RunPlacement Placement =>
        new(Origin is null ? null : JsonSerializer.Deserialize<RunOrigin>(Origin, ContractJson.Options), Reference, ComparedWith);

    public IReadOnlyList<string> Tags => JsonSerializer.Deserialize<string[]>(TagsJson) ?? [];

    public RunStatus State => Enum.Parse<RunStatus>(Status);
}
