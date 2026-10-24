using System.CommandLine;
using Suwageval.Cli.AgentDelete;
using Suwageval.Cli.Agents;
using Suwageval.Cli.Evaluation.AgentErrors;
using Suwageval.Cli.Evaluation.Cancel;
using Suwageval.Cli.Evaluation.Compare;
using Suwageval.Cli.Evaluation.Groups;
using Suwageval.Cli.Evaluation.Note;
using Suwageval.Cli.Evaluation.Push;
using Suwageval.Cli.Evaluation.Recheck;
using Suwageval.Cli.Evaluation.Rerun;
using Suwageval.Cli.Evaluation.Results;
using Suwageval.Cli.Evaluation.Run;
using Suwageval.Cli.Evaluation.Runs;
using Suwageval.Cli.Evaluation.Scenarios;
using Suwageval.Cli.Evaluation.Sweep;
using Suwageval.Cli.Evaluation.Trend;
using Suwageval.Cli.Evaluation.Unstable;
using Suwageval.Cli.Evaluation.Validate;
using Suwageval.Cli.Files;
using Suwageval.Cli.Init;
using Suwageval.Cli.Monitoring.Conversations;
using Suwageval.Cli.Output;
using Suwageval.Cli.Versions;

namespace Suwageval.Cli;

/// <summary>The command tree. Streams come in as arguments so the tests can talk to it without the console.</summary>
internal static class CliApp
{
    // http: the client the commands reach the outside with (the API for push, run and results, the agent for validate); null makes their own.
    // openBrowser: how results, compare and versions diff open a page; null is the system's browser.
    // interactive: whether a person answers on input; without one, nothing is asked.
    // origins: how run reads the repository and the CI environment; null is the real ones.
    // shell, labelPollInterval: how sweep runs its command lines and how often it reads the agent's label; null is the real shell and 2 s.
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
        ShellCommand? shell = null,
        TimeSpan? labelPollInterval = null,
        CancellationToken interrupt = default
    )
    {
        var root = Create(new Terminal(input, interactive), input, http, openBrowser, origins, shell, labelPollInterval);
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

    /// <summary>The command tree, its help listed by section; <see cref="Run"/> parses and invokes it.</summary>
    public static RootCommand Create(
        Terminal terminal,
        TextReader input,
        HttpClient? http,
        Action<Uri>? openBrowser,
        RunOriginReader? origins,
        ShellCommand? shell,
        TimeSpan? labelPollInterval
    )
    {
        var root = new RootCommand("Suwageval: the evaluation files of an agent's repository.")
        {
            InitCommand.Create(terminal),
            ValidateCommand.Create(input, http),
            PushCommand.Create(http),
            RunCommand.Create(terminal, http, origins),
            RerunCommand.Create(terminal, http),
            RecheckCommand.Create(terminal, http),
            CancelCommand.Create(http),
            NoteCommand.Create(http),
            ResultsCommand.Create(http, openBrowser),
            CompareCommand.Create(http, openBrowser),
            SweepCommand.Create(terminal, http, origins, shell, labelPollInterval),
            RunsCommand.Create(http),
            AgentsCommand.Create(http),
            AgentCommand.Create(http),
            ScenariosCommand.Create(http),
            ScenarioCommand.Create(http),
            TrendCommand.Create(http),
            UnstableCommand.Create(http),
            AgentErrorsCommand.Create(http),
            VersionsCommand.Create(terminal, http, openBrowser),
            GroupsCommand.Create(terminal, http),
            AgentDeleteCommand.Create(terminal, http),
            new Command("monitoring", "The conversations of the deployed agent (features.monitoring in agent.yaml).")
            {
                ConversationsCommand.List(http),
                ConversationsCommand.Show(http),
            },
        };
        HelpSections.Apply(root);
        return root;
    }
}
