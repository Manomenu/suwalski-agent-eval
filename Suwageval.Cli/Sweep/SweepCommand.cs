using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Sweep;

/// <summary>
/// <c>suwageval sweep [variant…]</c>: runs the scenarios of the agent of <c>suwageval/agent.yaml</c> once per variant, as the file's
/// <c>sweep:</c> section says (<see cref="SweepSettings"/>), all in one run group, then prints a row per variant and the link to the
/// group's variants page. It exits 0 when every variant was run to its end (failing checks are results, not failures), 2 when one could
/// not be (reconfiguring, the label, the run) or <c>restore</c> failed, 130 after Ctrl+C. The files are not pushed: run <c>push</c> first.
/// </summary>
internal static partial class SweepCommand
{
    private static readonly TimeSpan CancelTimeout = TimeSpan.FromSeconds(10);

    /// <param name="terminal">Where <c>--select</c> asks which variants to run.</param>
    /// <param name="http">The client to the API and the agent; none means one for each (the tests pass their own).</param>
    /// <param name="origins">Reads where the agent's repository stands; none means the real one and the CI environment (the tests pass their own).</param>
    /// <param name="shell">Runs the command lines; none means the real shell (the tests pass their own).</param>
    /// <param name="pollInterval">How often the label is read while waiting for it; none means every 2 s.</param>
    public static Command Create(
        Terminal terminal,
        HttpClient? http = null,
        RunOriginReader? origins = null,
        ShellCommand? shell = null,
        TimeSpan? pollInterval = null
    )
    {
        var variants = new Argument<string[]>("variant")
        {
            Description = "The variants to run, as named in the variants folder. Default: all of them.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var select = new Option<bool>("--select")
        {
            Description =
                "Instead of naming variants, pick them from a numbered list of the variants folder (answer \"1,3\", \"2-4\" or \"all\"). Needs a terminal.",
        };
        var tag = new Option<string[]>("--tag")
        {
            Description = "Only the scenarios with this tag; repeat it to take the scenarios with any of the tags. Default every scenario.",
            AllowMultipleArgumentsPerToken = false,
        };
        var group = new Option<string?>("--group")
        {
            Description = "The run group of the sweep (lowercase kebab-case). Default: sweep-<yyyymmdd-hhmm>-<agent>.",
        };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = AgentEndpoint.OptionDescription("the endpointUrl of agent.yaml (the contract's label is read from there too)."),
        };
        var reference = new Option<string?>("--ref") { Description = "Compare with the reference runs of this branch." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "sweep",
            "Run the agent's scenarios once per variant (the sweep: section of agent.yaml), in one run group, and print the rows and the link that compares them."
        )
        {
            variants,
            select,
            tag,
            group,
            endpoint,
            reference,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                RunAsync(
                    terminal,
                    new Arguments(
                        parsed.GetRequiredValue(dir),
                        parsed.GetValue(variants) ?? [],
                        parsed.GetValue(select),
                        parsed.GetValue(tag) ?? [],
                        parsed.GetValue(group),
                        AgentEndpoint.Override(parsed.GetValue(endpoint)),
                        parsed.GetValue(reference),
                        parsed.GetValue(server)
                    ),
                    http,
                    origins ?? RunOriginReader.System,
                    shell ?? ShellProcess.RunAsync,
                    pollInterval ?? TimeSpan.FromSeconds(2),
                    parsed.InvocationConfiguration.Output,
                    parsed.InvocationConfiguration.Error,
                    cancellationToken
                )
        );
        return command;
    }

    private sealed record Arguments(
        string Dir,
        string[] Variants,
        bool Select,
        string[] Tags,
        string? Group,
        string? EndpointOverride,
        string? Reference,
        string? Server
    );

    private static async Task<int> RunAsync(
        Terminal terminal,
        Arguments arguments,
        HttpClient? http,
        RunOriginReader origins,
        ShellCommand shell,
        TimeSpan pollInterval,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        if (arguments.Select && arguments.Variants.Length > 0)
        {
            await error.WriteLineAsync("Name the variants or use --select, not both.");
            return 2;
        }
        if (arguments.Select && !terminal.Interactive)
        {
            await error.WriteLineAsync("--select asks on a terminal and input is redirected here: name the variants instead.");
            return 2;
        }
        var agent = AgentReader.Read(arguments.Dir, error);
        if (agent is null)
        {
            return 2;
        }
        if (agent.Sweep is not { } settings)
        {
            await error.WriteLineAsync(
                $"{Path.Combine(arguments.Dir, "agent.yaml")} has no 'sweep:' section: see the CLI's documentation."
            );
            return 2;
        }
        var endpointUrl = arguments.EndpointOverride ?? agent.Definition.EndpointUrl;
        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https"))
        {
            await error.WriteLineAsync($"'{endpointUrl}' is not an http or https address for the agent.");
            return 2;
        }
        var group = arguments.Group ?? DefaultGroup(agent.Definition.Name);
        var chosen = ChooseVariants(Path.Combine(arguments.Dir, settings.VariantsDir), arguments.Variants, group, out var problem);
        if (chosen is not null && arguments.Select)
        {
            chosen = await SelectAsync(chosen, terminal, output, error, cancellationToken);
            problem = "";
        }
        if (chosen is null)
        {
            if (problem.Length > 0)
            {
                await error.WriteLineAsync(problem);
            }
            return 2;
        }

        using var agentHttp = http is null ? new HttpClient { Timeout = ContractFetcher.Timeout } : null;
        return await ServerCall.RunAsync(
            arguments.Server,
            http,
            error,
            async (client, site) =>
            {
                var sweep = new VariantSweep(
                    settings,
                    shell,
                    async token => (await ContractFetcher.FetchAsync(http ?? agentHttp!, endpoint, token)).Contract?.Label,
                    token => StartRunAsync(client, agent.Definition.Name, arguments, group, origins, token),
                    output,
                    TimeSpan.FromSeconds(settings.WaitSeconds),
                    pollInterval
                );
                var result = await sweep.RunAsync(chosen, group, cancellationToken);
                await output.WriteLineAsync();
                await output.WriteAsync(Table(result, group));
                if (result.Outcomes.Any(outcome => outcome.RunId is not null))
                {
                    await output.WriteLineAsync($"Compare: {ServerAddress.VariantsPage(site, [], group, agent.Definition.Name)}");
                }
                return result.Interrupted ? RunOutput.Interrupted
                    : result.Failed ? 2
                    : 0;
            },
            agentDir: arguments.Dir
        );
    }

    private static async Task<VariantOutcome> StartRunAsync(
        ServerClient client,
        string agent,
        Arguments arguments,
        string group,
        RunOriginReader origins,
        CancellationToken cancellationToken
    )
    {
        RunCreated? created = null;
        try
        {
            created = await client.CreateAsync(
                new RunRequest
                {
                    Agent = agent,
                    Tags = arguments.Tags,
                    Group = group,
                    EndpointUrl = arguments.EndpointOverride,
                    Origin = origins.Read(arguments.Dir, arguments.Reference),
                },
                cancellationToken
            );
            await client.WaitForEndAsync(created.RunId, cancellationToken);
            var results = await client.ResultsAsync(created.RunId, cancellationToken);
            return new VariantOutcome("", created.RunId, RunVerdict.Line(results), results.Run.Status != RunStatus.Done);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && created is not null)
        {
            using var timeout = new CancellationTokenSource(CancelTimeout);
            await client.CancelAsync(created.RunId, timeout.Token);
            throw;
        }
        catch (ServerClientException failure)
        {
            return new VariantOutcome("", created?.RunId, failure.Message, true);
        }
    }

    /// <summary>Lists the variants and reads the answer, asking again until it is valid; null when the input ends first.</summary>
    private static async Task<List<string>?> SelectAsync(
        List<string> available,
        Terminal terminal,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        await output.WriteLineAsync("Variants:");
        await output.WriteLineAsync(VariantSelection.Menu(available));
        while (true)
        {
            await output.WriteAsync("Run which? (e.g. 1,3 or all): ");
            await output.FlushAsync(cancellationToken);
            var answer = await terminal.Input.ReadLineAsync(cancellationToken);
            if (answer is null)
            {
                await error.WriteLineAsync("No variants selected: the input ended.");
                return null;
            }
            var selected = VariantSelection.Parse(answer, available, out var problem);
            if (selected is not null)
            {
                return selected;
            }
            await error.WriteLineAsync(problem);
        }
    }

    private static string DefaultGroup(string agent) =>
        $"sweep-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}-{agent}";

    /// <summary>The variants to run: the named ones, or every file of the folder; null with <paramref name="problem"/> when that is not possible.</summary>
    private static List<string>? ChooseVariants(string variantsDir, string[] named, string group, out string problem)
    {
        problem = "";
        if (RunGroupRules.Problem(group) is { } groupProblem)
        {
            problem = groupProblem;
            return null;
        }
        var available = Directory.Exists(variantsDir)
            ? Directory
                .EnumerateFiles(variantsDir)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
        if (available.Count == 0)
        {
            problem = $"No variants: {variantsDir} is missing or has no files.";
            return null;
        }
        // A name goes into command lines, so it is a plain word.
        if (available.FirstOrDefault(name => !VariantName().IsMatch(name)) is { } odd)
        {
            problem = $"'{odd}' in {variantsDir} cannot be a variant: use letters, digits, '.', '_' and '-'.";
            return null;
        }
        if (named.FirstOrDefault(name => !available.Contains(name)) is { } unknown)
        {
            problem = $"No such variant: {unknown}. Available: {string.Join(", ", available)}";
            return null;
        }
        return named.Length == 0 ? available : [.. named.Distinct(StringComparer.Ordinal)];
    }

    private static string Table(SweepResult result, string group)
    {
        var variantWidth = result.Outcomes.Select(outcome => outcome.Variant.Length).Prepend("variant".Length).Max();
        var table = new StringBuilder();
        table.AppendLine(CultureInfo.InvariantCulture, $"Sweep {group}");
        table.AppendLine(CultureInfo.InvariantCulture, $"{"variant".PadRight(variantWidth)}  {"run", -36}  result");
        foreach (var outcome in result.Outcomes)
        {
            table.AppendLine(
                CultureInfo.InvariantCulture,
                $"{outcome.Variant.PadRight(variantWidth)}  {outcome.RunId?.ToString() ?? "-", -36}  {outcome.Summary}"
            );
        }
        if (result.RestoreFailed)
        {
            table.AppendLine("restore failed: the agent may still serve the last variant.");
        }
        return table.ToString();
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex VariantName();
}
