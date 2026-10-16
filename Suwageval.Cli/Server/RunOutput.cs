using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

/// <summary>What <c>run</c> and <c>rerun</c> print once the server has queued a run, and the exit code they give.</summary>
internal static class RunOutput
{
    public static async Task<int> ReportAsync(
        RunClient client,
        RunCreated created,
        bool wait,
        bool json,
        Uri site,
        TextWriter output,
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

        await client.WaitForEndAsync(created.RunId, cancellationToken);
        var results = await client.ResultsAsync(created.RunId, cancellationToken);
        if (json)
        {
            await output.WriteLineAsync(OutputJson.Of(results));
        }
        else
        {
            await output.WriteLineAsync(RunVerdict.Line(results));
            await output.WriteLineAsync(ServerAddress.RunPage(site, created.RunId).ToString());
        }
        return RunVerdict.ExitCode(results);
    }
}
