using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Output;

/// <summary>What <c>run</c>, <c>rerun</c> and <c>recheck</c> print once the server has queued a run, and the exit code they give.</summary>
internal static class RunOutput
{
    /// <summary>The exit code of a wait interrupted with Ctrl+C while the run goes on on the server (128 + SIGINT, as shells report it).</summary>
    public const int Interrupted = 130;

    /// <summary>How long the request to cancel the run may take once the waiting was interrupted.</summary>
    private static readonly TimeSpan CancelTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Prints the version and, without <paramref name="wait"/>, the run's id; with it, waits for the end and prints the totals.
    /// <paramref name="strict"/> gives the old exit code (1 for any failed check) instead of failing only on regressions and new failures.
    /// <paramref name="cancellationToken"/> is cancelled by Ctrl+C: while waiting, that stops the waiting and asks on
    /// <paramref name="terminal"/> whether to cancel the run too.
    /// </summary>
    public static async Task<int> ReportAsync(
        ServerClient client,
        RunCreated created,
        bool wait,
        bool json,
        bool strict,
        Uri site,
        TextWriter output,
        TextWriter error,
        Terminal terminal,
        CancellationToken cancellationToken
    )
    {
        if (!json)
        {
            // The version is what the agent served just now; "new" means its contract differs from every earlier one.
            await output.WriteLineAsync($"version {created.AgentVersionId}: {(created.VersionCreated ? "new" : "existing")}");
        }
        if (!wait)
        {
            await output.WriteLineAsync(json ? OutputJson.Of(created) : created.RunId.ToString());
            return 0;
        }

        RunResults results;
        try
        {
            await client.WaitForEndAsync(created.RunId, cancellationToken);
            results = await client.ResultsAsync(created.RunId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await InterruptedAsync(client, created.RunId, site, error, terminal);
        }
        if (json)
        {
            await output.WriteLineAsync(OutputJson.Of(results));
        }
        else
        {
            await output.WriteLineAsync(
                results.Run.Status == RunStatus.Done && results.Regression is not null
                    ? RegressionText.Describe(results)
                    : RunVerdict.Line(results)
            );
            await output.WriteLineAsync(ServerAddress.RunPage(site, created.RunId).ToString());
        }
        return RunVerdict.ExitCode(results, strict);
    }

    /// <summary>
    /// Runs <paramref name="action"/>, which starts a run; Ctrl+C before it ends leaves the run as it is (it may exist on the server already).
    /// </summary>
    /// <param name="action">Starts the run and reports on it; its exit code.</param>
    /// <param name="error">Where the interruption is said.</param>
    /// <param name="what">What was being started: "run" or "re-check".</param>
    /// <param name="cancellationToken">Cancelled by Ctrl+C.</param>
    public static async Task<int> GuardAsync(Func<Task<int>> action, TextWriter error, string what, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            return await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync($"Interrupted; if the {what} was started, `suwageval runs` lists it.");
            return Interrupted;
        }
    }

    /// <summary>
    /// Ctrl+C stopped the waiting, not the run: the server would go on with nobody watching. A person is asked whether to cancel it
    /// too; without anyone to ask (a CI job being stopped), it is cancelled, since nobody would read its results. Everything goes to
    /// the error stream, so <c>--json</c> output stays JSON.
    /// </summary>
    private static async Task<int> InterruptedAsync(ServerClient client, Guid runId, Uri site, TextWriter error, Terminal terminal)
    {
        await error.WriteLineAsync();
        var cancel = true;
        if (terminal.Interactive)
        {
            await error.WriteAsync($"Cancel run {runId} on the server too? [y/N] ");
            var answer = await terminal.Input.ReadLineAsync(CancellationToken.None);
            cancel = answer?.Trim().ToUpperInvariant() is "Y" or "YES";
        }
        if (!cancel)
        {
            await error.WriteLineAsync($"Run {runId} goes on; stop it with: suwageval cancel {runId}");
            await error.WriteLineAsync(ServerAddress.RunPage(site, runId).ToString());
            return Interrupted;
        }
        using var timeout = new CancellationTokenSource(CancelTimeout);
        await client.CancelAsync(runId, timeout.Token);
        await error.WriteLineAsync($"Cancelling run {runId}.");
        await error.WriteLineAsync(ServerAddress.RunPage(site, runId).ToString());
        return 2;
    }
}
