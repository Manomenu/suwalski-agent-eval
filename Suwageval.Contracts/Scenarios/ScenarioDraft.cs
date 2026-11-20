namespace Suwageval.Contracts.Scenarios;

public sealed record ScenarioDraft
{
    public required string Key { get; init; }

    public required string Path { get; init; }

    public required string Yaml { get; init; }

    public string? Notice { get; init; }
}
