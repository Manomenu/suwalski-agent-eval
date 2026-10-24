using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Files;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Cli.Validation;

namespace Suwageval.Cli.Evaluation.Push;

/// <summary><c>suwageval push [dir]</c>: validates the agent's files, then writes the agent, its scenarios and checks to the server (its version is not pushed: a run reads it from the agent).</summary>
internal static class PushCommand
{
    /// <param name="http">The client to reach the server with; null makes one for <c>--server</c> (tests pass the in-process server's).</param>
    public static Command Create(HttpClient? http)
    {
        var dir = new Argument<string>("dir")
        {
            Description = "The suwageval folder with agent.yaml and scenarios/.",
            DefaultValueFactory = _ => AgentFolder.DefaultDir,
        };
        var server = CommandOptions.Server();
        var json = CommandOptions.Json("Print one JSON object with the result instead of text.");

        var command = new Command(
            "push",
            "Validate the agent's files, then write the agent, its scenarios and checks to the server. Pushing the same files again changes nothing. (Versions are not pushed: a run reads them from the running agent.)"
        )
        {
            dir,
            server,
            json,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                RunAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    parsed.GetValue(json),
                    http,
                    parsed.InvocationConfiguration,
                    cancellationToken
                )
        );
        return command;
    }

    private static async Task<int> RunAsync(
        string dir,
        string? server,
        bool json,
        HttpClient? http,
        InvocationConfiguration console,
        CancellationToken cancellationToken
    )
    {
        var folder = AgentFolder.Read(dir);
        var validation = Validator.Validate(folder);
        if (validation is not { IsValid: true, Agent: { } agent })
        {
            if (json)
            {
                await console.Output.WriteLineAsync(ValidationReport.Json(validation));
            }
            else
            {
                await console.Error.WriteAsync(ValidationReport.Text(validation));
            }
            return 1;
        }

        return await ServerCall.RunAsync(
            server,
            http,
            console.Error,
            async (client, _) =>
            {
                var (savedAgent, agentCreated) = await client.PutAgentAsync(agent.Definition, cancellationToken);
                // With evaluation off the server keeps no scenarios: they stay in the repository, unsent.
                var evaluation = agent.Definition.Features?.Evaluation ?? true;
                var scenarioResults = evaluation
                    ? (
                        await client.PutScenariosAsync(
                            savedAgent.Name,
                            [.. validation.LoadedScenarios.Select(s => s.Scenario)],
                            cancellationToken
                        )
                    ).Results
                    : [];
                var checkResults = evaluation
                    ? (
                        await client.PutChecksAsync(
                            savedAgent.Name,
                            [.. validation.LoadedScenarios.SelectMany(s => s.Checks)],
                            cancellationToken
                        )
                    ).Results
                    : [];
                // With monitoring on, the file is the whole set of the agent's monitoring checks (none without one).
                var monitoring = agent.Definition.Features?.Monitoring is not null;
                var monitoringResults = monitoring
                    ? (await client.PutMonitoringChecksAsync(savedAgent.Name, validation.MonitoringChecks ?? [], cancellationToken)).Results
                    : [];
                var result = new PushResult(
                    savedAgent,
                    agentCreated,
                    scenarioResults,
                    checkResults,
                    evaluation ? 0 : validation.LoadedScenarios.Count
                )
                {
                    MonitoringChecks = monitoringResults,
                    MonitoringChecksNotSent = monitoring ? 0 : validation.MonitoringChecks?.Count ?? 0,
                };
                await console.Output.WriteAsync(json ? OutputJson.Of(result) + Environment.NewLine : Text(result));
                return 0;
            },
            failureExit: 1,
            agentDir: dir
        );
    }

    private static string Text(PushResult result)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine($"agent {result.Agent.Name}: {(result.AgentCreated ? "created" : "existing")}");
        if (result.ScenariosNotSent > 0)
        {
            writer.WriteLine($"{result.ScenariosNotSent} scenarios not pushed: evaluation is off (features.evaluation in agent.yaml)");
        }
        foreach (var scenario in result.Scenarios)
        {
            writer.WriteLine($"scenario {scenario.Key}: {Name(scenario.Outcome)}");
        }
        foreach (var check in result.Checks)
        {
            writer.WriteLine($"check {check.Key}: {Name(check.Outcome)}");
        }
        foreach (var check in result.MonitoringChecks)
        {
            writer.WriteLine($"monitoring check {check.Key}: {Name(check.Outcome)}");
        }
        if (result.MonitoringChecksNotSent > 0)
        {
            writer.WriteLine(
                $"{result.MonitoringChecksNotSent} monitoring checks not pushed: monitoring is off (features.monitoring in agent.yaml)"
            );
        }
        return writer.ToString();
    }

    private static string Name(Contracts.UpsertOutcome outcome) => outcome.ToString().ToLowerInvariant();
}
