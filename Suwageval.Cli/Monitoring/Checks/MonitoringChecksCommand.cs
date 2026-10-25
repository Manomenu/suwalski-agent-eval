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
    /// <param name="openBrowser">Opens a page for <c>--open</c>; none means the system's default browser (the tests record the page instead).</param>
    public static Command List(HttpClient? http = null, Action<Uri>? openBrowser = null)
    {
        var environment = new Option<string?>("--environment") { Description = "The rates over this environment's conversations only." };
        var json = CommandOptions.Json("Print the checks as JSON.");
        var open = CommandOptions.Open("the agent's Monitoring page (narrowed to --environment)");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("checks", "List the agent's monitoring checks with their pass rates over the last 7 and 30 days.")
        {
            environment,
            json,
            open,
            server,
            dir,
        };
        CommandOptions.RefuseJsonWithOpen(command, json, open);
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, site) =>
                    {
                        if (parsed.GetValue(open))
                        {
                            await WebPage.ShowAsync(
                                ServerAddress.MonitoringPage(site, agent, parsed.GetValue(environment)),
                                parsed.InvocationConfiguration.Output,
                                parsed.InvocationConfiguration.Error,
                                openBrowser
                            );
                            return 0;
                        }
                        var checks = await client.MonitoringChecksAsync(agent, parsed.GetValue(environment), cancellationToken);
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

    public static Command Trend(HttpClient? http = null, Action<Uri>? openBrowser = null)
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
        var open = CommandOptions.Open("the agent's Monitoring page, where the trend is drawn (narrowed to --environment)");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("trend", "Day by day: the conversations, how many were checked, and each monitoring check's pass rate.")
        {
            days,
            environment,
            json,
            open,
            server,
            dir,
        };
        CommandOptions.RefuseJsonWithOpen(command, json, open);
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, site) =>
                    {
                        if (parsed.GetValue(open))
                        {
                            await WebPage.ShowAsync(
                                ServerAddress.MonitoringPage(site, agent, parsed.GetValue(environment)),
                                parsed.InvocationConfiguration.Output,
                                parsed.InvocationConfiguration.Error,
                                openBrowser
                            );
                            return 0;
                        }
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
        foreach (var date in trend.Days.Select(day => day.Date).Distinct())
        {
            var days = trend.Days.Where(day => day.Date == date).ToList();
            var environments =
                days.Count > 1 ? $" ({string.Join(", ", days.Select(day => $"{day.Environment} {day.Conversations}"))})" : "";
            var checks = trend
                .Points.Where(point => point.Date == date)
                .GroupBy(point => point.CheckKey)
                .Select(group => $"{group.Key} {Rate(new CheckRate(group.Sum(point => point.Passed), group.Sum(point => point.Checked)))}");
            lines.Add(
                string.Create(
                        CultureInfo.InvariantCulture,
                        $"{date}  {days.Sum(day => day.Conversations)} conversations{environments}  {days.Sum(day => day.Checked)} checked  {string.Join("  ", checks)}"
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
