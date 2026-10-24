using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Monitoring;

/// <summary>
/// Where a monitored conversation stands: <c>Open</c> while its turns come, <c>Closed</c> once none came for the agent's
/// <c>features.monitoring.closeAfter</c> (a turn after that opens it again).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConversationState>))]
public enum ConversationState
{
    Open,
    Closed,
}
