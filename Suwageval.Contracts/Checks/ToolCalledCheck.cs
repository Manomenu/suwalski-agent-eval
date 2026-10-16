using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

public sealed record ToolCalledCheck : CheckInput
{
    public required string Tool { get; init; }

    /// <summary>The LLM agent that must have made the call; a call that names no agent never satisfies it.</summary>
    public string? By { get; init; }

    /// <summary>A subset the call's arguments must contain. Free JSON, shaped by the tool's parameters.</summary>
    public JsonElement? Args { get; init; }

    /// <summary>
    /// A subset the matching call's result must contain: the content of the <c>ToolResult</c> that answers the call, parsed as JSON
    /// (content that is not JSON counts as the JSON string of itself). A call with no result never matches.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }
}
