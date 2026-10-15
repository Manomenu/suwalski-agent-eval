namespace Suwageval.Contracts.Checks;

public sealed record ToolNotCalledCheck : CheckInput
{
    public required string Tool { get; init; }
}
