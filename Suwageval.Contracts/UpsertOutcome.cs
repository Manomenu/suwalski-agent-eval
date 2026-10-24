using System.Text.Json.Serialization;

namespace Suwageval.Contracts;

/// <summary>
/// What a push did to one scenario or check, serialized by name: <c>created</c>, <c>updated</c>, <c>unchanged</c>; <c>deleted</c> for a
/// monitoring check the file no longer has.
/// </summary>
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
