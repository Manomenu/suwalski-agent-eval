using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Problems;

internal static class ProblemsCommand
{
    internal sealed record Found(IReadOnlyList<UnstableCheck> UnstableChecks, IReadOnlyList<AgentError> AgentErrors);

    public static Command Create(HttpClient? http = null, Action<Uri>? openBrowser = null)
    {
        var json = CommandOptions.Json("Print the unstable checks and the agent errors as JSON.");
        var versions = CommandOptions.Versions();
        var dataVersions = CommandOptions.DataVersions();
        var open = CommandOptions.Open(
            "the agent's Evaluation page, where the problems are listed (no filter of this command applies there)"
        );
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "problems",
            "List the agent's open problems beyond one run: unstable checks (pass in some runs of a version, fail in others) and agent errors (scenarios the agent gave no answer in)."
        )
        {
            json,
            open,
            versions,
            dataVersions,
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
                        var picked = parsed.GetValue(versions) ?? [];
                        var data = parsed.GetValue(dataVersions) ?? [];
                        var unstable = await client.UnstableChecksAsync(agent, picked, data, cancellationToken);
                        var errors = await client.AgentErrorsAsync(agent, picked, data, cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(new Found(unstable, errors)));
                            return 0;
                        }
                        if (unstable.Count == 0 && errors.Count == 0)
                        {
                            await output.WriteLineAsync("No unstable checks and no agent errors.");
                            return 0;
                        }
                        if (unstable.Count > 0)
                        {
                            await output.WriteLineAsync("Unstable checks (a failure there may be noise):");
                            foreach (var check in unstable)
                            {
                                await output.WriteLineAsync($"  {Line(check)}");
                            }
                        }
                        if (errors.Count > 0)
                        {
                            await output.WriteLineAsync("Agent errors (no answer, so the checks were not evaluated):");
                            foreach (var error in errors)
                            {
                                await output.WriteLineAsync($"  {Line(error)}");
                            }
                        }
                        return 0;
                    }
                )
        );
        return command;
    }

    private static string Line(UnstableCheck check) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{check.ScenarioKey}/{check.CheckKey}  {check.PassedRuns}/{check.Runs}  version {check.AgentVersionId}{Label(check.VersionLabel)}"
        );

    private static string Line(AgentError error) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{error.ScenarioKey}  {error.Errors}/{error.Samples}  version {error.AgentVersionId}{Label(error.VersionLabel)}  {OneLine(error.NewestError)}"
        );

    private static string Label(string? label) => string.IsNullOrWhiteSpace(label) ? "" : $" ({label})";

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
