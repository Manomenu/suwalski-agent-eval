using System.Text.Json;
using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Agents;

/// <summary>A row of <c>agents</c>.</summary>
internal sealed record AgentRow(
    Guid Id,
    string Name,
    string EndpointUrl,
    DateTime CreatedAt,
    string? RegressionBranches = null,
    int? RegressionHistory = null
)
{
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
