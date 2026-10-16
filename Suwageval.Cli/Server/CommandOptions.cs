using System.CommandLine;
using System.CommandLine.Parsing;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Server;

/// <summary>The options and arguments several commands have in common, so each is described once.</summary>
internal static class CommandOptions
{
    public static Option<string?> Server() => new("--server") { Description = ServerAddress.OptionDescription };

    /// <summary>The folder with <c>agent.yaml</c>, for the commands that name the agent from it.</summary>
    public static Option<string> Dir() =>
        new("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => AgentFolder.DefaultDir };

    public static Option<bool> Json(string description) => new("--json") { Description = description };

    /// <param name="what">What is waited for: "run" or "re-check".</param>
    public static Option<bool> Wait(string what) =>
        new("--wait")
        {
            Description =
                $"Wait for the {what} to finish and print its totals and the link to its page (see the exit codes). Ctrl+C asks whether to cancel the run too.",
        };

    /// <summary>The ids in the tokens of an argument; every token that is not one is an error, named by <paramref name="noun"/> ("run", "version").</summary>
    public static List<Guid> ParseIds(ArgumentResult result, string noun)
    {
        ArgumentNullException.ThrowIfNull(result);
        var ids = new List<Guid>();
        foreach (var token in result.Tokens)
        {
            if (Guid.TryParse(token.Value, out var id))
            {
                ids.Add(id);
            }
            else
            {
                result.AddError($"'{token.Value}' is not a {noun} id.");
            }
        }
        return ids;
    }
}
