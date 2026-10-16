using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Cli.Validation;

namespace Suwageval.Cli.Push;

/// <summary><c>suwageval push [dir]</c>: validates the agent's files, then writes the agent, its scenarios and checks to the server (its version is not pushed: a run reads it from the agent).</summary>
internal static class PushCommand
{
    private const string DefaultDir = "suwageval";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="http">The client to reach the server with; null makes one for <c>--server</c> (tests pass the in-process server's).</param>
    public static Command Create(HttpClient? http)
    {
        var dir = new Argument<string>("dir")
        {
            Description = "The suwageval folder with agent.yaml and scenarios/.",
            DefaultValueFactory = _ => DefaultDir,
        };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };
        var json = new Option<bool>("--json") { Description = "Print one JSON object with the result instead of text." };

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
        if (!validation.IsValid)
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

        var problems = new List<Problem>();
        var agent = AgentFileReader.Read(folder.AgentFile, folder.AgentText!, problems);
        var scenarios = folder
            .Scenarios.Select(file => ScenarioFileReader.Read(file.File, file.Text, problems))
            .OfType<LoadedScenario>()
            .ToList();
        if (agent is null)
        {
            // Validation passed a moment ago on the same text, so this only happens if the readers and the validator disagree.
            await console.Error.WriteLineAsync("The agent file could not be read.");
            return 1;
        }

        using var created = http is null ? new HttpClient { BaseAddress = ServerAddress.Api(ServerAddress.Site(server)) } : null;
        var client = new SuwagevalClient(http ?? created!);
        try
        {
            var (savedAgent, agentCreated) = await client.PutAgentAsync(agent.Definition, cancellationToken);
            var scenarioResults = await client.PutScenariosAsync(
                savedAgent.Name,
                [.. scenarios.Select(s => s.Scenario)],
                cancellationToken
            );
            var checkResults = await client.PutChecksAsync(savedAgent.Name, [.. scenarios.SelectMany(s => s.Checks)], cancellationToken);
            var result = new PushResult(savedAgent, agentCreated, scenarioResults.Results, checkResults.Results);
            await console.Output.WriteAsync(json ? JsonSerializer.Serialize(result, JsonOptions) + Environment.NewLine : Text(result));
            return 0;
        }
        catch (ServerException error)
        {
            await console.Error.WriteLineAsync(error.Message);
            return 1;
        }
    }

    private static string Text(PushResult result)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine($"agent {result.Agent.Name}: {(result.AgentCreated ? "created" : "existing")}");
        foreach (var scenario in result.Scenarios)
        {
            writer.WriteLine($"scenario {scenario.Key}: {Name(scenario.Outcome)}");
        }
        foreach (var check in result.Checks)
        {
            writer.WriteLine($"check {check.Key}: {Name(check.Outcome)}");
        }
        return writer.ToString();
    }

    private static string Name(Contracts.UpsertOutcome outcome) => outcome.ToString().ToLowerInvariant();
}
