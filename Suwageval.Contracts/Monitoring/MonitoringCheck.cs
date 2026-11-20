using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Monitoring;

public sealed record MonitoringCheck
{
    public required int Revision { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required CheckInput Check { get; init; }

    public required CheckRate Last7Days { get; init; }

    public required CheckRate Last30Days { get; init; }
}
