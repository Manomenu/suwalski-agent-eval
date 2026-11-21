using Microsoft.AspNetCore.Http.HttpResults;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents;

internal sealed record DeletionResult<T>(DeletionOutcome Outcome, T? Contents)
    where T : class
{
    public IResult ToHttp(IResult notFound, DeletionRefusal? notArchived, DeletionRefusal hasActiveRuns) =>
        Outcome switch
        {
            DeletionOutcome.NotFound => notFound,
            DeletionOutcome.NotArchived when notArchived is not null => Refuse(notArchived),
            DeletionOutcome.HasActiveRuns => Refuse(hasActiveRuns),
            DeletionOutcome.Allowed => Results.Ok(Contents),
            DeletionOutcome.Deleted => Results.NoContent(),
            _ => throw new InvalidOperationException($"Unexpected deletion outcome {Outcome}."),
        };

    private static ProblemHttpResult Refuse(DeletionRefusal refusal) =>
        ProblemResults.Conflict(refusal.Code, refusal.Title, refusal.Detail);
}
