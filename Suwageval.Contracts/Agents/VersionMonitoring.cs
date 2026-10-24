namespace Suwageval.Contracts.Agents;

/// <summary>
/// A version in one monitored environment: the conversations whose last turn it answered in the last 30 days, when it last answered a turn,
/// and whether it is the version answering there now (the one of the environment's newest turn).
/// </summary>
public sealed record VersionMonitoring(string Environment, int Conversations30Days, DateTimeOffset LastConversationAt, bool Current);
