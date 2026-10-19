using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Agents;

/// <summary>How a line of a diff stands.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DiffKind>))]
public enum DiffKind
{
    Same,
    Added,
    Removed,
}
