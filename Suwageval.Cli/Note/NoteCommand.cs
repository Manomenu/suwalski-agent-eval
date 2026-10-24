using System.CommandLine;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Note;

/// <summary>
/// <c>suwageval note &lt;runId&gt; &lt;text&gt;</c> sets the note of a run (why it was started), <c>--clear</c> removes it. Works for a run in
/// any status and changes no verdict. Prints the run's page; exits 0, 2 for an unknown run or a server that does not answer, and
/// refuses a note the rules (<see cref="RunNoteRules"/>) do not allow before calling the server.
/// </summary>
internal static class NoteCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var runId = new Argument<Guid>("runId") { Description = "The run, as `suwageval run` printed it." };
        var text = new Argument<string?>("text")
        {
            Description = $"The new note, free text (at most {RunNoteRules.MaxLength} characters).",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var clear = new Option<bool>("--clear") { Description = "Remove the note instead of setting one." };
        var server = CommandOptions.Server();

        var command = new Command("note", "Set or clear the note of a run: why it was started. It changes no verdict.")
        {
            runId,
            text,
            clear,
            server,
        };
        command.Validators.Add(result =>
        {
            var given = result.GetValue(text);
            if (result.GetValue(clear) == !string.IsNullOrWhiteSpace(given))
            {
                result.AddError("Give the note's text or --clear, not both and not neither.");
            }
            else if (RunNoteRules.Problem(given) is { } problem)
            {
                result.AddError(problem);
            }
        });
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, site) =>
                    {
                        var output = parsed.InvocationConfiguration.Output;
                        var id = parsed.GetRequiredValue(runId);
                        await client.SetNoteAsync(id, parsed.GetValue(clear) ? null : parsed.GetValue(text), cancellationToken);
                        await output.WriteLineAsync(
                            parsed.GetValue(clear) ? $"Cleared the note of run {id}." : $"Set the note of run {id}."
                        );
                        await output.WriteLineAsync(ServerAddress.RunPage(site, id).ToString());
                        return 0;
                    }
                )
        );
        return command;
    }
}
