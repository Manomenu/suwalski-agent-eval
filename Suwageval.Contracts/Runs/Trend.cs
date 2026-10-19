using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>How something stands against the baseline's: better, worse, or the same.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Trend>))]
public enum Trend
{
    Same,
    Better,
    Worse,
}
