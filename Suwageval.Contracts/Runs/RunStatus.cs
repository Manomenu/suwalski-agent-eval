using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>
/// Where a run stands. <c>Failed</c> is the run itself breaking down; a scenario the agent did not answer is a result,
/// not a failure. <c>Interrupted</c> is the server stopping while the run was going (a restart, a deploy): the samples stored so far are
/// kept, nothing is wrong with the agent, and a new run (<c>run</c>, or "Run again" on its page) does it again.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus
{
    Queued,
    Running,
    Done,
    Failed,
    Cancelled,
    Interrupted,
}
