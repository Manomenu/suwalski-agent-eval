using System.CommandLine;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Evaluation.Trend;

internal static class TrendCommand
{
    public static Command Create(HttpClient? http = null, Action<Uri>? openBrowser = null)
    {
        var json = CommandOptions.Json("Print the trend's points as JSON.");
        var open = CommandOptions.Open("the agent's Evaluation page, where the trend is drawn");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "trend",
            "Show the agent's pass-rate trend, run by run (runs started in a group are experiments and not included)."
        )
        {
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
                                ServerAddress.EvaluationPage(site, agent),
                                parsed.InvocationConfiguration.Output,
                                parsed.InvocationConfiguration.Error,
                                openBrowser
                            );
                            return 0;
                        }
                        var points = await client.PassRatesAsync(agent, cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(points));
                            return 0;
                        }
                        if (points.Count == 0)
                        {
                            await output.WriteLineAsync("No finished runs.");
                        }
                        foreach (var point in points.OrderBy(point => point.CreatedAt))
                        {
                            await output.WriteLineAsync(TrendText.Line(point));
                        }
                        return 0;
                    }
                )
        );
        return command;
    }
}
