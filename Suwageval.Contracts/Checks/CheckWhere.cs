namespace Suwageval.Contracts.Checks;

public sealed record CheckWhere
{
    public TurnSelector Turn { get; init; }

    public string? After { get; init; }

    public string? Before { get; init; }
}
