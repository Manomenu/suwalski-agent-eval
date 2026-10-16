using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>
/// Where a run stands. <c>Failed</c> is the run itself breaking down; a scenario the agent did not answer is a result,
/// not a failure.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus
{
    Queued,
    Running,
    Done,
    Failed,
    Cancelled,
}
