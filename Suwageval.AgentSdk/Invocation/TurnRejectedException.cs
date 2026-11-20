namespace Suwageval.AgentSdk.Invocation;

public sealed class TurnRejectedException(string reason) : Exception(reason);
