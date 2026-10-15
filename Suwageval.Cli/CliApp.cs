using System.CommandLine;
using Suwageval.Cli.Validate;

namespace Suwageval.Cli;

/// <summary>The command tree. Streams come in as arguments so the tests can talk to it without the console.</summary>
internal static class CliApp
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        var root = new RootCommand("Suwageval: the evaluation files of an agent's repository.") { ValidateCommand.Create(input) };
        return root.Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
    }
}
