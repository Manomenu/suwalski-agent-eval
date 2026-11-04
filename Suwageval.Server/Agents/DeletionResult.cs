using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents;

internal sealed record DeletionResult<T>(DeletionOutcome Outcome, T? Contents)
    where T : class
{
    public IResult ToHttp(string what, string name, DeletionRefusal? notArchived, DeletionRefusal hasActiveRuns) =>
        Outcome switch
        {
            DeletionOutcome.NotFound => ProblemResults.NotFound(what, name),
            DeletionOutcome.NotArchived when notArchived is not null => ProblemResults.Conflict(notArchived.Title, notArchived.Detail),
            DeletionOutcome.HasActiveRuns => ProblemResults.Conflict(hasActiveRuns.Title, hasActiveRuns.Detail),
            DeletionOutcome.Allowed => Results.Ok(Contents),
            DeletionOutcome.Deleted => Results.NoContent(),
            _ => throw new InvalidOperationException($"Unexpected outcome {Outcome} deleting a {what.ToLowerInvariant()}."),
        };
}
