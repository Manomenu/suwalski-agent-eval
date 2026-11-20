using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

public sealed record ToolCalledCheck : CheckInput
{
    public required string Tool { get; init; }

    public string? By { get; init; }

    public JsonElement? Args { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }
}
