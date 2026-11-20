namespace Suwageval.Contracts.Agents;

public sealed record RegressionSettings
{
    public const int DefaultHistory = 5;

    public const int MinHistory = 1;

    public const int MaxHistory = 20;

    public IReadOnlyList<string>? Branches { get; init; }

    public int? History { get; init; }
}
