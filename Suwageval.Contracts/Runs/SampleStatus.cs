using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<SampleStatus>))]
public enum SampleStatus
{
    Done,

    Failed,

    NotSimulated,
}
