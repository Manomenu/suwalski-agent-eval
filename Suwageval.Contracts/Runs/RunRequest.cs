namespace Suwageval.Contracts.Runs;

public sealed record RunRequest
{
    public required string Agent { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? EndpointUrl { get; init; }

    public string? Group { get; init; }

    public string? Note { get; init; }

    public bool Gate { get; init; }

    public RunOrigin? Origin { get; init; }
}
