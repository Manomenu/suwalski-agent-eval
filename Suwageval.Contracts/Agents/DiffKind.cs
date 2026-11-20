using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Agents;

[JsonConverter(typeof(JsonStringEnumConverter<DiffKind>))]
public enum DiffKind
{
    Same,
    Added,
    Removed,
}
