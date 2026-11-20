namespace Suwageval.Contracts.Agents;

public sealed record VersionMonitoring(string Environment, int Conversations30Days, DateTimeOffset LastConversationAt, bool Current);
