using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Gate;

internal static class GateCommand
{
    public static Command Create(Terminal terminal, HttpClient? http = null, RunOriginReader? origins = null)
    {
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = AgentEndpoint.OptionDescription(
                "the endpointUrl the agent was registered with (its contract is read from there too)."
            ),
        };
        var reference = new Option<string?>("--ref")
        {
            Description = "The branch the run says it comes from, instead of the one read from the repository.",
        };
        var note = CommandOptions.RunNote();
        var json = CommandOptions.Json("Print the run's results as JSON (its gate field is the verdict).");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "gate",
            "Run every scenario with an acGuardrail until its first passing attempt, as the server has them: run `suwageval push` first. Quiet: one line when every acceptance criterion passed (exit 0), the failures in full when one failed (exit 1); 2 when the gate cannot start or finish."
        )
        {
            endpoint,
            reference,
            note,
            json,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var error = parsed.InvocationConfiguration.Error;
                var agentDir = parsed.GetRequiredValue(dir);
                var agent = AgentReader.Read(agentDir, error)?.Definition.Name;
                if (agent is null)
                {
                    return Task.FromResult(2);
                }
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    error,
                    (client, site) =>
                        RunOutput.GuardAsync(
                            async () =>
                            {
                                var created = await client.CreateAsync(
                                    new RunRequest
                                    {
                                        Agent = agent,
                                        Gate = true,
                                        Note = parsed.GetValue(note),
                                        EndpointUrl = AgentEndpoint.Override(parsed.GetValue(endpoint)),
                                        Origin = (origins ?? RunOriginReader.System).Read(agentDir, parsed.GetValue(reference)),
                                    },
                                    cancellationToken
                                );
                                var (results, interrupted) = await RunOutput.WaitAsync(
                                    client,
                                    created.RunId,
                                    site,
                                    error,
                                    terminal,
                                    cancellationToken
                                );
                                if (results is null)
                                {
                                    return interrupted;
                                }
                                if (parsed.GetValue(json))
                                {
                                    await output.WriteLineAsync(OutputJson.Of(results));
                                }
                                else
                                {
                                    foreach (var line in GateText.Lines(results))
                                    {
                                        await output.WriteLineAsync(line);
                                    }
                                }
                                return GateText.ExitCode(results);
                            },
                            error,
                            "gate run",
                            cancellationToken
                        ),
                    agentDir: agentDir
                );
            }
        );
        return command;
    }
}
