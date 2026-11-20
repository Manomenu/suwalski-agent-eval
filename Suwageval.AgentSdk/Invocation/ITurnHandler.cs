namespace Suwageval.AgentSdk.Invocation;

public interface ITurnHandler
{
    Task<TurnResponse> HandleAsync(TurnRequest request, CancellationToken cancellationToken);
}
