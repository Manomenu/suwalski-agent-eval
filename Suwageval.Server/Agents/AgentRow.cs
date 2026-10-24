using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Agents;

/// <summary>A row of <c>agents</c>.</summary>
internal sealed record AgentRow(
    Guid Id,
    string Name,
    string EndpointUrl,
    DateTime CreatedAt,
    string? RegressionBranches = null,
    int? RegressionHistory = null,
    int? Samples = null,
    bool Evaluation = true,
    string? Monitoring = null
)
{
    /// <summary>The areas the agent was pushed with: evaluation on unless switched off, monitoring with its settings or off.</summary>
    public AgentFeatures Features =>
        new()
        {
            Evaluation = Evaluation,
            Monitoring = Monitoring is null ? null : JsonSerializer.Deserialize<MonitoringSettings>(Monitoring, ContractJson.Options),
        };

    /// <summary>The regression settings the agent was pushed with (the branches arrive as a JSON array); none when it names neither branches nor a history.</summary>
    public RegressionSettings? Regression =>
        RegressionBranches is null && RegressionHistory is null
            ? null
            : new RegressionSettings
            {
                Branches = RegressionBranches is null ? null : JsonSerializer.Deserialize<string[]>(RegressionBranches),
                History = RegressionHistory,
            };
}
