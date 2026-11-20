namespace Suwageval.Contracts.Checks;

public sealed record ToolNotCalledCheck : CheckInput
{
    public required string Tool { get; init; }

    public string? By { get; init; }
}
