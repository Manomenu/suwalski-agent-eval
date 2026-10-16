namespace Suwageval.Contracts.Checks;

public sealed record ToolNotCalledCheck : CheckInput
{
    public required string Tool { get; init; }

    /// <summary>Only calls made by this LLM agent count; a call that names no agent is never one of them.</summary>
    public string? By { get; init; }
}
