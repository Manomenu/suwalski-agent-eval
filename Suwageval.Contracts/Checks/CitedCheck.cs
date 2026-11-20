namespace Suwageval.Contracts.Checks;

public sealed record CitedCheck : CheckInput
{
    public required IReadOnlyList<string> Sources { get; init; }
}
