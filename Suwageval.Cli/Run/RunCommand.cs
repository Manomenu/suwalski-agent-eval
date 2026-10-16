using System.CommandLine;
using System.Text.Json;
using Suwageval.Cli.Files;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Run;

/// <summary>
/// <c>suwageval run</c>: starts a run of the agent named in <c>suwageval/agent.yaml</c> on the server, which reads the
/// agent's contract and runs against that version (the output says which, and whether it is new). Exit code with
/// <c>--wait</c>: 0 when every check reached its threshold, 1 when one did not, 2 when the run could not be started,
/// failed or was cancelled.
/// </summary>
internal static class RunCommand
{
    private const string DefaultDir = "suwageval";
    private const string DefaultServer = "http://localhost:6230";
    private const string ServerVariable = "SUWAGEVAL_URL";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions Pretty = new(ContractJson.Options)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="http">The client to the server; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var tag = new Option<string?>("--tag") { Description = "Only the scenarios with this tag; default every scenario." };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = "Reach the agent here (its contract is read from here too) instead of at the endpointUrl it was registered with.",
        };
        var wait = new Option<bool>("--wait") { Description = "Wait for the run to finish and print its results (see the exit codes)." };
        var json = new Option<bool>("--json") { Description = "Print JSON: the results with --wait, otherwise { runId }." };
        var server = new Option<string?>("--server")
        {
            Description = $"The server's address; default ${ServerVariable} or {DefaultServer}.",
        };
        var dir = new Option<string>("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => DefaultDir };

        var command = new Command(
            "run",
            "Run the agent's scenarios on the server. With --wait: exit 0 when all checks pass, 1 when a check fails, 2 when the run cannot finish."
        )
        {
            tag,
            endpoint,
            wait,
            json,
            server,
            dir,
        };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var error = parsed.InvocationConfiguration.Error;
                var agent = ReadAgentName(parsed.GetRequiredValue(dir), error);
                if (agent is null)
                {
                    return 2;
                }
                var transport = http ?? new HttpClient { BaseAddress = ServerAddress(parsed.GetValue(server)) };
                try
                {
                    return await ExecuteAsync(
                        new RunClient(transport),
                        new RunRequest
                        {
                            Agent = agent,
                            Tag = parsed.GetValue(tag),
                            EndpointUrl = parsed.GetValue(endpoint),
                        },
                        parsed.GetValue(wait),
                        parsed.GetValue(json),
                        output,
                        cancellationToken
                    );
                }
                catch (RunClientException failure)
                {
                    await error.WriteLineAsync(failure.Message);
                    return 2;
                }
                finally
                {
                    if (http is null)
                    {
                        transport.Dispose();
                    }
                }
            }
        );
        return command;
    }

    private static async Task<int> ExecuteAsync(
        RunClient client,
        RunRequest request,
        bool wait,
        bool json,
        TextWriter output,
        CancellationToken cancellationToken
    )
    {
        var created = await client.CreateAsync(request, cancellationToken);
        if (!json)
        {
            // The version is what the agent served just now; "new" means its contract differs from every earlier one.
            await output.WriteLineAsync($"version {created.AgentVersionId}: {(created.VersionCreated ? "new" : "existing")}");
        }
        if (!wait)
        {
            await output.WriteLineAsync(json ? JsonSerializer.Serialize(created, Pretty) : created.RunId.ToString());
            return 0;
        }

        RunSummary run;
        while (true)
        {
            run = await client.GetAsync(created.RunId, cancellationToken);
            if (run.Status is RunStatus.Done or RunStatus.Failed or RunStatus.Cancelled)
            {
                break;
            }
            await Task.Delay(PollInterval, cancellationToken);
        }

        var results = await client.ResultsAsync(created.RunId, cancellationToken);
        await output.WriteAsync(json ? JsonSerializer.Serialize(results, Pretty) + Environment.NewLine : RunReportText.Format(results));
        if (run.Status != RunStatus.Done)
        {
            await output.WriteLineAsync(
                json ? "" : $"The run {run.Status.ToString().ToLowerInvariant()}{(run.Error is null ? "." : $": {run.Error}")}"
            );
            return 2;
        }
        return RunReportText.AllPassed(results) ? 0 : 1;
    }

    private static string? ReadAgentName(string dir, TextWriter error)
    {
        var file = Path.Combine(dir, "agent.yaml");
        if (!File.Exists(file))
        {
            error.WriteLine($"{file} not found: run this in an agent's repository, or point --dir at its suwageval folder.");
            return null;
        }
        var problems = new List<Problem>();
        var agent = AgentFileReader.Read(file, File.ReadAllText(file), problems);
        foreach (var problem in problems)
        {
            error.WriteLine($"{problem.File}: {problem.Message}");
        }
        return agent?.Definition.Name;
    }

    private static Uri ServerAddress(string? option)
    {
        var address = option ?? Environment.GetEnvironmentVariable(ServerVariable) ?? DefaultServer;
        // A base address needs its trailing slash, or the last path segment is replaced by a relative request.
        return new Uri(address.TrimEnd('/') + "/");
    }
}
