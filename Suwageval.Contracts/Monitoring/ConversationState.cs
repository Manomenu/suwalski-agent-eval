using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Monitoring;

[JsonConverter(typeof(JsonStringEnumConverter<ConversationState>))]
public enum ConversationState
{
    Open,
    Closed,
}
