using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<RunGrouping>))]
public enum RunGrouping
{
    All,
    None,
    Any,
}
