using System.CommandLine;
using System.CommandLine.Parsing;
using Suwageval.Cli.Files;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

internal static class CommandOptions
{
    public static Option<string?> Server() => new("--server") { Description = ServerAddress.OptionDescription };

    public static Option<string> Dir() =>
        new("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => AgentFolder.DefaultDir };

    public static Option<bool> Strict() =>
        new("--strict")
        {
            Description =
                "Exit 1 when any check fails its threshold, not only on a regression or a new failing check (known, already broken and unstable failures count).",
        };

    public static Option<Guid[]> Versions() =>
        new("--version")
        {
            Description = "Decide over this agent version id only (repeatable; default: every version not archived).",
            DefaultValueFactory = _ => [],
        };

    public static Option<string[]> DataVersions() =>
        new("--data-version")
        {
            Description =
                "Only the runs on this retrieval data version, the dataVersion the agent declared when the run started (repeatable).",
            DefaultValueFactory = _ => [],
            AllowMultipleArgumentsPerToken = false,
        };

    public static Option<string?> RunGroup() =>
        new("--group")
        {
            Description =
                "Put the run in this group (lowercase kebab-case, e.g. sweep-2026-10-21): the runs of one sweep, listed with `runs --group` and compared side by side. A group run is an experiment: no regression states, never a reference run, not in the trend; with --wait it exits 1 when any check fails its threshold (as --strict).",
        };

    public static Option<string?> RunNote()
    {
        var note = new Option<string?>("--note")
        {
            Description =
                $"Why the run is started, free text (at most {NoteRules.MaxLength} characters). Shown on the run page, in `runs` and in comparisons; change it later with `suwageval note`. It changes no verdict.",
        };
        note.Validators.Add(result =>
        {
            if (NoteRules.Problem(result.GetValueOrDefault<string?>()) is { } problem)
            {
                result.AddError(problem);
            }
        });
        return note;
    }

    public static Option<bool> Open(string page) => new("--open") { Description = $"Open {page} on the web instead of printing." };

    public static void RefuseJsonWithOpen(Command command, Option<bool> json, Option<bool> open)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validators.Add(result =>
        {
            if (result.GetValue(json) && result.GetValue(open))
            {
                result.AddError("--json and --open cannot be used together.");
            }
        });
    }

    public static Option<string?> Group(string description) => new("--group") { Description = description };

    public static Option<bool> Json(string description) => new("--json") { Description = description };

    public static Option<bool> Wait(string what) =>
        new("--wait")
        {
            Description =
                $"Wait for the {what} to finish and print its totals and the link to its page (see the exit codes). Ctrl+C asks whether to cancel the run too.",
        };

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

    public static async Task<int> RefuseAsync(TextWriter error, string reason)
    {
        await error.WriteLineAsync(reason);
        return 2;
    }
}
