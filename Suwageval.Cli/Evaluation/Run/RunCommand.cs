using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Run;

internal static class RunCommand
{
    public static Command Create(Terminal terminal, HttpClient? http = null, RunOriginReader? origins = null)
    {
        var tag = new Option<string[]>("--tag")
        {
            Description = "Only the scenarios with this tag; repeat it to take the scenarios with any of the tags. Default every scenario.",
            AllowMultipleArgumentsPerToken = false,
        };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = AgentEndpoint.OptionDescription(
                "the endpointUrl the agent was registered with (its contract is read from there too)."
            ),
        };
        var reference = new Option<string?>("--ref")
        {
            Description = "Compare with the reference runs of this branch, instead of the one the run picks from the repository.",
        };
        var group = CommandOptions.RunGroup();
        var note = CommandOptions.RunNote();
        var wait = CommandOptions.Wait("run");
        var json = CommandOptions.Json("Print JSON: the results with --wait, otherwise { runId }.");
        var strict = CommandOptions.Strict();
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "run",
            "Run the agent's scenarios on the server. With --wait: exit 0 unless a check regressed or is new and failing (--strict: unless any check fails), 2 when the run cannot finish."
        )
        {
            tag,
            endpoint,
            reference,
            group,
            note,
            wait,
            json,
            strict,
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
                                        Tags = parsed.GetValue(tag) ?? [],
                                        Group = parsed.GetValue(group),
                                        Note = parsed.GetValue(note),
                                        EndpointUrl = AgentEndpoint.Override(parsed.GetValue(endpoint)),
                                        Origin = (origins ?? RunOriginReader.System).Read(agentDir, parsed.GetValue(reference)),
                                    },
                                    cancellationToken
                                );
                                return await RunOutput.ReportAsync(
                                    client,
                                    created,
                                    parsed.GetValue(wait),
                                    parsed.GetValue(json),
                                    parsed.GetValue(strict),
                                    site,
                                    output,
                                    error,
                                    terminal,
                                    cancellationToken
                                );
                            },
                            error,
                            "run",
                            cancellationToken
                        ),
                    agentDir: agentDir
                );
            }
        );
        return command;
    }
}
