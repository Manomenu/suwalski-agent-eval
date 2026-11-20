namespace Suwageval.Contracts.Runs;

public sealed record RunPassRates
{
    public required Guid RunId { get; init; }

    public required DateTime CreatedAt { get; init; }

    public Guid? RecheckedBy { get; init; }

    public required Guid AgentVersionId { get; init; }

    public IReadOnlyList<string> LimitedToTags { get; init; } = [];

    public string? VersionLabel { get; init; }

    public string? DataVersion { get; init; }

    public required IReadOnlyList<string> ModelNames { get; init; }

    public required double PassRate { get; init; }

    public double? MedianMs { get; init; }

    public double? P95Ms { get; init; }

    public required IReadOnlyList<TagPassRate> Tags { get; init; }
}
