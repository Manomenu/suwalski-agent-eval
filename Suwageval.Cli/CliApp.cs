using System.CommandLine;
using Suwageval.Cli.Push;
using Suwageval.Cli.Results;
using Suwageval.Cli.Run;
using Suwageval.Cli.Validate;

namespace Suwageval.Cli;

/// <summary>The command tree. Streams come in as arguments so the tests can talk to it without the console.</summary>
internal static class CliApp
{
    // http: the client the commands reach the outside with (the API for push, run and results, the agent for validate); null makes their own.
    // openBrowser: how results opens a page; null is the system's browser.
    public static int Run(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        HttpClient? http = null,
        Action<Uri>? openBrowser = null
    )
    {
        var root = new RootCommand("Suwageval: the evaluation files of an agent's repository.")
        {
            ValidateCommand.Create(input, http),
            PushCommand.Create(http),
            RunCommand.Create(http),
            ResultsCommand.Create(http, openBrowser),
        };
        return root.Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
    }
}
