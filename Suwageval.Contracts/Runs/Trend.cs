using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<Trend>))]
public enum Trend
{
    Same,
    Better,
    Worse,
}
