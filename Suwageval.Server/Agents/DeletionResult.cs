using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents;

/// <summary>A refusal the client sees as 409: its title and what to do.</summary>
internal sealed record DeletionRefusal(string Title, string Detail);

/// <summary>The outcome of deleting an agent, version or run group and, unless it was not found, what it holds (or held).</summary>
internal sealed record DeletionResult<T>(DeletionOutcome Outcome, T? Contents)
    where T : class
{
    /// <summary>
    /// The answer for the request: 404 for <paramref name="what"/> <paramref name="name"/>, 409 with the given refusals, 200 with the contents
    /// of a dry run, 204 once deleted. A thing that is never archived (an agent) passes no <paramref name="notArchived"/>.
    /// </summary>
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
