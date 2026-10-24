using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Cli.Validation;

namespace Suwageval.Cli.Evaluation.Validate;

/// <summary>
/// <c>suwageval validate [path]</c>: checks an agent's files. The structure needs no server and no agent; tool and event
/// names are checked against the contract the running agent serves, when it can be reached.
/// </summary>
internal static class ValidateCommand
{
    private const string StdinName = "<stdin>";

    /// <param name="input">Where a scenario read from stdin comes from.</param>
    /// <param name="http">The client to reach the agent with; null makes one with a short timeout (tests pass a stub).</param>
    public static Command Create(TextReader input, HttpClient? http = null)
    {
        var path = new Argument<string>("path")
        {
            Description = "The suwageval folder, one scenario file, or '-' for one scenario read from stdin.",
            DefaultValueFactory = _ => AgentFolder.DefaultDir,
        };
        var dir = new Option<string>("--dir")
        {
            Description = "The folder with agent.yaml, for a single scenario file or stdin.",
            DefaultValueFactory = _ => AgentFolder.DefaultDir,
        };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = AgentEndpoint.OptionDescription("the endpointUrl of agent.yaml."),
        };
        var offline = new Option<bool>("--offline") { Description = "Do not ask the agent for its contract: check the structure only." };
        var json = new Option<bool>("--json")
        {
            Description = "Print { valid, namesChecked, notice, problems: [{ file, path, message }] } instead of text.",
        };

        var command = new Command(
            "validate",
            "Check the agent's files against the contract. Tool and event names are checked against the contract the running agent serves (GET /suwageval/contract); if it cannot be reached they are not, and that is said. Exit code 1 when anything is wrong."
        )
        {
            path,
            dir,
            endpoint,
            offline,
            json,
        };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                using var created = http is null && !parsed.GetValue(offline) ? new HttpClient { Timeout = ContractFetcher.Timeout } : null;
                var result = await RunAsync(
                    parsed.GetRequiredValue(path),
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(endpoint),
                    parsed.GetValue(offline),
                    input,
                    http ?? created,
                    cancellationToken
                );
                var output = parsed.InvocationConfiguration.Output;
                await output.WriteAsync(
                    parsed.GetValue(json) ? ValidationReport.Json(result) + Environment.NewLine : ValidationReport.Text(result)
                );
                return result.IsValid ? 0 : 1;
            }
        );
        return command;
    }

    private static async Task<ValidationResult> RunAsync(
        string path,
        string dir,
        string? endpoint,
        bool offline,
        TextReader input,
        HttpClient? http,
        CancellationToken cancellationToken
    )
    {
        string agentFile;
        string? agentText;
        IReadOnlyList<SourceText> scenarios;
        AgentFolder? folder = null;
        if (path == "-" || File.Exists(path))
        {
            agentFile = AgentFile(dir);
            agentText = ReadIfExists(agentFile);
            scenarios =
            [
                path == "-"
                    ? new SourceText(StdinName, await input.ReadToEndAsync(cancellationToken))
                    : new SourceText(path, await File.ReadAllTextAsync(path, cancellationToken)),
            ];
        }
        else if (Directory.Exists(path))
        {
            // The whole folder: its monitoring checks too.
            folder = AgentFolder.Read(path);
            (agentFile, agentText, scenarios) = (folder.AgentFile, folder.AgentText, folder.Scenarios);
        }
        else
        {
            return new ValidationResult([new Problem(path, "", "No such file or folder.", null, null)], 0, 0);
        }

        folder ??= new AgentFolder(agentFile, agentText, scenarios);
        var structure = Validator.Validate(folder);
        if (!structure.IsValid)
        {
            return structure;
        }
        if (offline || http is null)
        {
            return structure with { Notice = "names not checked: --offline" };
        }

        var source = new List<Problem>();
        var url = AgentEndpoint.Override(endpoint) ?? AgentFileReader.Read(agentFile, agentText!, source)?.Definition.EndpointUrl;
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https"))
        {
            return structure with { Notice = $"names not checked: '{url}' is not an http or https address for the agent" };
        }
        var (contract, failure) = await ContractFetcher.FetchAsync(http, address, cancellationToken);
        if (contract is null)
        {
            return structure with { Notice = $"names not checked: {failure}" };
        }
        return Validator.Validate(folder, contract) with { NamesChecked = true };
    }

    private static string AgentFile(string dir) => Path.Combine(dir, "agent.yaml");

    private static string? ReadIfExists(string file) => File.Exists(file) ? File.ReadAllText(file) : null;
}
