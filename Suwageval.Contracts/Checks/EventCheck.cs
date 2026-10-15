using System.Text.Json;

namespace Suwageval.Contracts.Checks;

public sealed record EventCheck : CheckInput
{
    /// <summary>The event's type, one the agent declares.</summary>
    public required string Event { get; init; }

    /// <summary>A subset the event's data must contain. Free JSON, shaped by the event's <c>dataSchema</c>.</summary>
    public JsonElement? Data { get; init; }
}
