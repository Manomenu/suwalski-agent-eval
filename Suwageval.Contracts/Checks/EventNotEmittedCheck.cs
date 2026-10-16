using System.Text.Json;

namespace Suwageval.Contracts.Checks;

public sealed record EventNotEmittedCheck : CheckInput
{
    /// <summary>The event's type, one the agent declares.</summary>
    public required string Event { get; init; }

    /// <summary>With it, only events whose data contains this subset count. Free JSON, shaped by the event's <c>dataSchema</c>.</summary>
    public JsonElement? Data { get; init; }
}
