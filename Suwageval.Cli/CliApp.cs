using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Suwageval.Cli.AgentDelete;
using Suwageval.Cli.Agents;
using Suwageval.Cli.Evaluation.Cancel;
using Suwageval.Cli.Evaluation.Compare;
using Suwageval.Cli.Evaluation.Gate;
using Suwageval.Cli.Evaluation.Groups;
using Suwageval.Cli.Evaluation.Note;
using Suwageval.Cli.Evaluation.Problems;
using Suwageval.Cli.Evaluation.Push;
using Suwageval.Cli.Evaluation.Recheck;
using Suwageval.Cli.Evaluation.Results;
using Suwageval.Cli.Evaluation.Run;
using Suwageval.Cli.Evaluation.Runs;
using Suwageval.Cli.Evaluation.Scenarios;
using Suwageval.Cli.Evaluation.Sweep;
using Suwageval.Cli.Evaluation.Trend;
using Suwageval.Cli.Evaluation.Validate;
using Suwageval.Cli.Files;
using Suwageval.Cli.Init;
using Suwageval.Cli.Monitoring.Checks;
using Suwageval.Cli.Monitoring.Conversations;
using Suwageval.Cli.Output;
using Suwageval.Cli.Versions;

namespace Suwageval.Cli;

internal static class CliApp
{
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The one synchronous root of a console application: Main and the tests call it, and nothing runs above it."
    )]
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
        // Why: System.CommandLine would otherwise end the process two seconds after Ctrl+C, in the middle of the question run --wait asks
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
            RecheckCommand.Create(terminal, http),
            GateCommand.Create(terminal, http, origins),
            CancelCommand.Create(http),
            NoteCommand.Create(http),
            ResultsCommand.Create(http, openBrowser),
            CompareCommand.Create(http, openBrowser),
            SweepCommand.Create(terminal, http, origins, shell, labelPollInterval),
            RunsCommand.Create(http, openBrowser),
            AgentsCommand.Create(http, openBrowser),
            AgentCommand.Create(http),
            ScenariosCommand.Create(http),
            ScenarioCommand.Create(http),
            TrendCommand.Create(http, openBrowser),
            ProblemsCommand.Create(http, openBrowser),
            VersionsCommand.Create(terminal, http, openBrowser),
            GroupsCommand.Create(terminal, http),
            AgentDeleteCommand.Create(terminal, http),
            new Command("monitoring", "The conversations of the deployed agent (features.monitoring in agent.yaml).")
            {
                ConversationsCommand.List(http, openBrowser),
                ConversationsCommand.Show(http, openBrowser),
                MonitoringChecksCommand.List(http, openBrowser),
                MonitoringChecksCommand.Trend(http, openBrowser),
            },
        };
        HelpSections.Apply(root);
        return root;
    }
}
