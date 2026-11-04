using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Output;

internal static class RunOutput
{
    public const int Interrupted = 130;

    private static readonly TimeSpan CancelTimeout = TimeSpan.FromSeconds(10);

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
            await output.WriteLineAsync($"version {created.AgentVersionId}: {(created.VersionCreated ? "new" : "existing")}");
        }
        if (!wait)
        {
            await output.WriteLineAsync(json ? OutputJson.Of(created) : created.RunId.ToString());
            return 0;
        }

        var (results, interrupted) = await WaitAsync(client, created.RunId, site, error, terminal, cancellationToken);
        if (results is null)
        {
            return interrupted;
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

    public static async Task<(RunResults? Results, int ExitCode)> WaitAsync(
        ServerClient client,
        Guid runId,
        Uri site,
        TextWriter error,
        Terminal terminal,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        try
        {
            await client.WaitForEndAsync(runId, cancellationToken);
            return (await client.ResultsAsync(runId, cancellationToken), 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (null, await InterruptedAsync(client, runId, site, error, terminal));
        }
    }

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

    // Why: without a person to ask (a CI job being stopped) the run is cancelled, since nobody would read its results
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
