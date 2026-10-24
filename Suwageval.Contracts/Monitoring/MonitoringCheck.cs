using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Monitoring;

/// <summary>A monitoring check of the agent with its latest revision and how it did on the conversations of the last 7 and 30 days.</summary>
public sealed record MonitoringCheck
{
    public required int Revision { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required CheckInput Check { get; init; }

    /// <summary>Over the conversations whose last turn came in the last 7 days (any revision of the check).</summary>
    public required CheckRate Last7Days { get; init; }

    public required CheckRate Last30Days { get; init; }
}
