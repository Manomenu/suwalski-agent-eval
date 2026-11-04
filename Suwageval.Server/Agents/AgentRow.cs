using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Agents;

internal sealed record AgentRow(
    Guid Id,
    string Name,
    string EndpointUrl,
    DateTime CreatedAt,
    string? RegressionBranches = null,
    int? RegressionHistory = null,
    int? Samples = null,
    bool Evaluation = true,
    string? Monitoring = null,
    int? AcGuardrailRetryLimit = null
)
{
    public AgentFeatures Features =>
        new()
        {
            Evaluation = Evaluation,
            Monitoring = Monitoring is null ? null : JsonSerializer.Deserialize<MonitoringSettings>(Monitoring, ContractJson.Options),
        };

    public RegressionSettings? Regression =>
        RegressionBranches is null && RegressionHistory is null
            ? null
            : new RegressionSettings
            {
                Branches = RegressionBranches is null ? null : JsonSerializer.Deserialize<string[]>(RegressionBranches),
                History = RegressionHistory,
            };
}
