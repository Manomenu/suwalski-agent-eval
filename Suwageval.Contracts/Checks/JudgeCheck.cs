namespace Suwageval.Contracts.Checks;

public sealed record JudgeCheck : CheckInput
{
    public required string Criteria { get; init; }
}
