using System.CommandLine;
using System.CommandLine.Parsing;
using Suwageval.Cli.Files;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

/// <summary>The options and arguments several commands have in common, so each is described once.</summary>
internal static class CommandOptions
{
    public static Option<string?> Server() => new("--server") { Description = ServerAddress.OptionDescription };

    /// <summary>The folder with <c>agent.yaml</c>, for the commands that name the agent from it.</summary>
    public static Option<string> Dir() =>
        new("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => AgentFolder.DefaultDir };

    public static Option<bool> Strict() =>
        new("--strict")
        {
            Description =
                "Exit 1 when any check fails its threshold, not only on a regression or a new failing check (known, already broken and unstable failures count).",
        };

    /// <summary>The agent versions a question is limited to; repeatable, none given: the server's default (every version not archived).</summary>
    public static Option<Guid[]> Versions() =>
        new("--version")
        {
            Description = "Decide over this agent version id only (repeatable; default: every version not archived).",
            DefaultValueFactory = _ => [],
        };

    /// <summary>The words of <c>--group</c> on <c>run</c> and <c>rerun</c>.</summary>
    public static Option<string?> RunGroup() =>
        new("--group")
        {
            Description =
                "Put the run in this group (lowercase kebab-case, e.g. sweep-2026-10-21): the runs of one sweep, listed with `runs --group` and compared side by side. A group run is an experiment: no regression states, never a reference run, not in the trend; with --wait it exits 1 when any check fails its threshold (as --strict).",
        };

    /// <summary>The words of <c>--note</c> on <c>run</c> and <c>rerun</c>; a note the rules refuse is an error before any request.</summary>
    public static Option<string?> RunNote()
    {
        var note = new Option<string?>("--note")
        {
            Description =
                $"Why the run is started, free text (at most {RunNoteRules.MaxLength} characters). Shown on the run page, in `runs` and in comparisons; change it later with `suwageval note`. It changes no verdict.",
        };
        note.Validators.Add(result =>
        {
            if (RunNoteRules.Problem(result.GetValueOrDefault<string?>()) is { } problem)
            {
                result.AddError(problem);
            }
        });
        return note;
    }

    public static Option<string?> Group(string description) => new("--group") { Description = description };

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

    /// <summary>A command that understood its arguments but will not do what they ask: the reason on stderr, exit 2.</summary>
    public static async Task<int> RefuseAsync(TextWriter error, string reason)
    {
        await error.WriteLineAsync(reason);
        return 2;
    }
}
