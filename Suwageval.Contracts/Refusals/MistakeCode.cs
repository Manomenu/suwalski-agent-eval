using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Refusals;

[JsonConverter(typeof(JsonStringEnumConverter<MistakeCode>))]
public enum MistakeCode
{
    [JsonStringEnumMemberName("field.invalid")]
    FieldInvalid,

    [JsonStringEnumMemberName("version.mismatch")]
    VersionMismatch,
}
