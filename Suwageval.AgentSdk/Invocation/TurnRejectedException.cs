namespace Suwageval.AgentSdk.Invocation;

/// <summary>
/// Thrown by an <see cref="ITurnHandler"/> for a conversation this agent cannot answer (an item kind it does not support, a
/// message over its own limit); <see cref="SuwagevalAgentEndpoints.MapSuwagevalAgent"/> answers it with a 400 that carries
/// <see cref="Exception.Message"/>.
/// </summary>
public sealed class TurnRejectedException(string reason) : Exception(reason);
