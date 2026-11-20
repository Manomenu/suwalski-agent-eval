using System.Text.Json.Serialization;

namespace Suwageval.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<UpsertOutcome>))]
public enum UpsertOutcome
{
    [JsonStringEnumMemberName("created")]
    Created,

    [JsonStringEnumMemberName("updated")]
    Updated,

    [JsonStringEnumMemberName("unchanged")]
    Unchanged,

    [JsonStringEnumMemberName("deleted")]
    Deleted,
}
