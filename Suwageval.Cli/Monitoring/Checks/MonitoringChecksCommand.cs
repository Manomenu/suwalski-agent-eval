using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Monitoring;

namespace Suwageval.Cli.Monitoring.Checks;

/// <summary>
/// <c>suwageval monitoring checks</c>: the agent's monitoring checks (pushed from <c>monitoring-checks.yaml</c>) with how they did on the
/// conversations of the last 7 and 30 days; <c>monitoring trend</c>: day by day, the conversations, how many were checked and each check's
/// pass rate. <c>--json</c> prints the records. Exit 0, or 2 when the agent file or the server is wrong (monitoring off too).
/// </summary>
internal static class MonitoringChecksCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command List(HttpClient? http = null)
    {
        var json = CommandOptions.Json("Print the checks as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("checks", "List the agent's monitoring checks with their pass rates over the last 7 and 30 days.")
        {
            json,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var checks = await client.MonitoringChecksAsync(agent, cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(checks));
                            return 0;
                        }
                        if (checks.Count == 0)
                        {
                            await output.WriteLineAsync("No monitoring checks: write them in suwageval/monitoring-checks.yaml and push.");
                            return 0;
                        }
                        foreach (var check in checks)
                        {
                            await output.WriteLineAsync(
                                $"{check.Check.Key}  7 days {Rate(check.Last7Days)}  30 days {Rate(check.Last30Days)}  {check.Check.Description}"
                            );
                        }
                        return 0;
                    }
                )
        );
        return command;
    }

    public static Command Trend(HttpClient? http = null)
    {
        var days = new Option<int>("--days") { Description = "How many days back (1 to 365).", DefaultValueFactory = _ => 30 };
        days.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 365)
            {
                result.AddError("--days is from 1 to 365.");
            }
        });
        var environment = new Option<string?>("--environment") { Description = "Only the conversations of this environment." };
        var json = CommandOptions.Json("Print the trend as JSON (what the web's charts draw).");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("trend", "Day by day: the conversations, how many were checked, and each monitoring check's pass rate.")
        {
            days,
            environment,
            json,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var trend = await client.MonitoringTrendAsync(
                            agent,
                            parsed.GetValue(days),
                            parsed.GetValue(environment),
                            cancellationToken
                        );
                        var output = parsed.InvocationConfiguration.Output;
                        await output.WriteLineAsync(parsed.GetValue(json) ? OutputJson.Of(trend) : Text(trend));
                        return 0;
                    }
                )
        );
        return command;
    }

    /// <summary>"80% (8/10)", or "-" when the check ran on no conversation.</summary>
    internal static string Rate(CheckRate rate) =>
        rate.Checked == 0 ? "-" : $"{PercentText.Of((double)rate.Passed / rate.Checked)} ({rate.Passed}/{rate.Checked})";

    internal static string Text(MonitoringTrend trend)
    {
        if (trend.Days.Count == 0)
        {
            return "No conversations in the period.";
        }
        var lines = new List<string>();
        foreach (var day in trend.Days)
        {
            var checks = trend
                .Points.Where(point => point.Date == day.Date)
                .GroupBy(point => point.CheckKey)
                .Select(group => $"{group.Key} {Rate(new CheckRate(group.Sum(point => point.Passed), group.Sum(point => point.Checked)))}");
            lines.Add(
                string.Create(
                        CultureInfo.InvariantCulture,
                        $"{day.Date}  {day.Conversations} conversations  {day.Checked} checked  {string.Join("  ", checks)}"
                    )
                    .TrimEnd()
            );
        }
        lines.AddRange(
            trend.Versions.Select(change =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"version {change.AgentVersionId}{RunLabelText.Suffix(change.Label)} first answered {change.At.UtcDateTime:yyyy-MM-dd HH:mm}Z"
                )
            )
        );
        lines.AddRange(
            trend.DataVersions.Select(change =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"data {change.DataVersion} first seen {change.At.UtcDateTime:yyyy-MM-dd HH:mm}Z"
                )
            )
        );
        return string.Join(Environment.NewLine, lines);
    }
}
