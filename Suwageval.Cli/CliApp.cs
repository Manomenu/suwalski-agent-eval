using System.CommandLine;
using Suwageval.Cli.AgentErrors;
using Suwageval.Cli.Cancel;
using Suwageval.Cli.Compare;
using Suwageval.Cli.Files;
using Suwageval.Cli.Init;
using Suwageval.Cli.Push;
using Suwageval.Cli.Recheck;
using Suwageval.Cli.Rerun;
using Suwageval.Cli.Results;
using Suwageval.Cli.Run;
using Suwageval.Cli.Runs;
using Suwageval.Cli.Server;
using Suwageval.Cli.Unstable;
using Suwageval.Cli.Validate;
using Suwageval.Cli.Versions;

namespace Suwageval.Cli;

/// <summary>The command tree. Streams come in as arguments so the tests can talk to it without the console.</summary>
internal static class CliApp
{
    // http: the client the commands reach the outside with (the API for push, run and results, the agent for validate); null makes their own.
    // openBrowser: how results, compare and versions diff open a page; null is the system's browser.
    // interactive: whether a person answers on input; without one, nothing is asked.
    // origins: how run reads the repository and the CI environment; null is the real ones.
    // interrupt: Ctrl+C (Program.cs). It cancels what a command does; run and rerun --wait ask then whether to cancel the run too.
    public static int Run(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        HttpClient? http = null,
        Action<Uri>? openBrowser = null,
        bool interactive = false,
        RunOriginReader? origins = null,
        CancellationToken interrupt = default
    )
    {
        var terminal = new Terminal(input, interactive);
        var root = new RootCommand("Suwageval: the evaluation files of an agent's repository.")
        {
            InitCommand.Create(terminal),
            ValidateCommand.Create(input, http),
            PushCommand.Create(http),
            RunCommand.Create(terminal, http, origins),
            RerunCommand.Create(terminal, http),
            RecheckCommand.Create(terminal, http),
            CancelCommand.Create(http),
            ResultsCommand.Create(http, openBrowser),
            CompareCommand.Create(http, openBrowser),
            RunsCommand.Create(http),
            UnstableCommand.Create(http),
            AgentErrorsCommand.Create(http),
            VersionsCommand.Create(http, openBrowser),
        };
        // No termination timeout: System.CommandLine would otherwise handle Ctrl+C itself and end the process two seconds later,
        // in the middle of the question run --wait asks. Ctrl+C reaches the commands through interrupt instead.
        return root.Parse(args)
            .InvokeAsync(
                new InvocationConfiguration
                {
                    Output = output,
                    Error = error,
                    ProcessTerminationTimeout = null,
                },
                interrupt
            )
            .GetAwaiter()
            .GetResult();
    }
}
