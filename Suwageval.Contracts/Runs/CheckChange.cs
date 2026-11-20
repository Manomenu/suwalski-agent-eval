using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<CheckChange>))]
public enum CheckChange
{
    Changed,
    Added,
    Removed,
}
