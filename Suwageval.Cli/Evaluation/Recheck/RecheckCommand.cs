using System.CommandLine;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Recheck;

/// <summary>
/// <c>suwageval recheck</c>: judges the stored answers of a finished run again with the current checks, without calling the agent.
/// It asks the server for the plan first: nothing is started when no check changed (exit 0, there is nothing to do) or when the run
/// cannot be re-checked (exit 2); <c>--dry-run</c> only prints the plan. Output and exit codes of a started re-check are those of <c>suwageval run</c>.
/// </summary>
internal static class RecheckCommand
{
    /// <param name="terminal">Where Ctrl+C during <c>--wait</c> asks whether to cancel the run too.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null)
    {
        var runId = new Argument<Guid>("runId") { Description = "The finished run whose answers to judge again." };
        var dryRun = new Option<bool>("--dry-run")
        {
            Description = "Print what would be re-checked (JSON with --json) and start nothing; exit 0.",
        };
        var wait = CommandOptions.Wait("re-check");
        var json = CommandOptions.Json("Print JSON: the plan with --dry-run, the results with --wait, otherwise { runId }.");
        var strict = CommandOptions.Strict();
        var server = CommandOptions.Server();

        var command = new Command(
            "recheck",
            "Judge the stored answers of a finished run again with the current checks, without calling the agent. Nothing is started when no check changed (exit 0). With --wait: exit 0 unless a check regressed or is new and failing (--strict: unless any check fails), 2 when the run cannot be re-checked or cannot finish."
        )
        {
            runId,
            dryRun,
            wait,
            json,
            strict,
            server,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var error = parsed.InvocationConfiguration.Error;
                var asJson = parsed.GetValue(json);
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    error,
                    (client, site) =>
                        RunOutput.GuardAsync(
                            async () =>
                            {
                                var plan = await client.RecheckPlanAsync(parsed.GetRequiredValue(runId), cancellationToken);
                                if (parsed.GetValue(dryRun) || !plan.Possible)
                                {
                                    await output.WriteLineAsync(
                                        asJson ? OutputJson.Of(plan) : RecheckText.Describe(parsed.GetRequiredValue(runId), plan)
                                    );
                                    return plan.Blocker is null or RecheckBlocker.NothingChanged || parsed.GetValue(dryRun) ? 0 : 2;
                                }
                                if (!asJson)
                                {
                                    foreach (var skipped in plan.SkippedScenarios)
                                    {
                                        await output.WriteLineAsync(RecheckText.Skipped(skipped));
                                    }
                                }
                                var created = await client.RecheckAsync(parsed.GetRequiredValue(runId), cancellationToken);
                                return await RunOutput.ReportAsync(
                                    client,
                                    created,
                                    parsed.GetValue(wait),
                                    asJson,
                                    parsed.GetValue(strict),
                                    site,
                                    output,
                                    error,
                                    terminal,
                                    cancellationToken
                                );
                            },
                            error,
                            "re-check",
                            cancellationToken
                        )
                );
            }
        );
        return command;
    }
}
