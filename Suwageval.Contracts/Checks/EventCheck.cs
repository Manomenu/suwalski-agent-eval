using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>
/// An event the agent emitted: by default at least one whose data contains <see cref="Data"/>. With a bound it counts those events;
/// with <see cref="Last"/> it compares only the last event of the type. The optional members are left out when not set, so a
/// check written before them keeps its content hash.
/// </summary>
public sealed record EventCheck : CheckInput
{
    /// <summary>The event's type, one the agent declares.</summary>
    public required string Event { get; init; }

    /// <summary>A subset the event's data must contain. Free JSON, shaped by the event's <c>dataSchema</c>.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>At least this many matching events, from 0; with only <see cref="Max"/> given it is 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Min { get; init; }

    /// <summary>At most this many matching events, from 1: none at all is an <c>event-not-emitted</c> check.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Max { get; init; }

    /// <summary>Exactly this many matching events, from 1; stands alone, without <see cref="Min"/> and <see cref="Max"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Exactly { get; init; }

    /// <summary>
    /// True: only the last event of the type in scope is compared with <see cref="Data"/>, e.g. an agent's state event read as where
    /// the conversation ended up.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Last { get; init; }
}
