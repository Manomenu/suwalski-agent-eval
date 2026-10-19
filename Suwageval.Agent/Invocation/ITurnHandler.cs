namespace Suwageval.Contracts.Invocation;

/// <summary>Answers <c>POST /suwageval/turn</c>: runs the agent on the conversation and returns everything it produced in that turn.</summary>
public interface ITurnHandler
{
    Task<TurnResponse> HandleAsync(TurnRequest request, CancellationToken cancellationToken);
}
