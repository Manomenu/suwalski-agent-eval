using System.Text.Json;

namespace Suwageval.Contracts.Checks;

public sealed record ToolCalledCheck : CheckInput
{
    public required string Tool { get; init; }

    /// <summary>A subset the call's arguments must contain. Free JSON, shaped by the tool's parameters.</summary>
    public JsonElement? Args { get; init; }
}
