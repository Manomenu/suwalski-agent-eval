using Suwageval.Cli.Files;

namespace Suwageval.Cli.Server;

/// <summary>What every command that talks to the server does around its request: makes the client and says what went wrong.</summary>
internal static class ServerCall
{
    /// <summary>
    /// Runs <paramref name="action"/> with a client for <c>--server</c> (or the <paramref name="http"/> a test injected, which is not
    /// disposed) and the site's address; its exit code. A refusal or an unreachable server is written to <paramref name="error"/>
    /// and gives <paramref name="failureExit"/>.
    /// </summary>
    public static async Task<int> RunAsync(
        string? serverOption,
        HttpClient? http,
        TextWriter error,
        Func<ServerClient, Uri, Task<int>> action,
        int failureExit = 2
    )
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(action);
        var site = ServerAddress.Site(serverOption);
        HttpClient? owned = null;
        try
        {
            return await action(new ServerClient(http ?? (owned = new HttpClient { BaseAddress = ServerAddress.Api(site) })), site);
        }
        catch (ServerClientException failure)
        {
            await error.WriteLineAsync(failure.Message);
            return failureExit;
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>Like <see cref="RunAsync"/> for a command about the agent named in <c>agent.yaml</c> in <paramref name="dir"/>; 2 when that cannot be read.</summary>
    public static async Task<int> ForAgentAsync(
        string dir,
        string? serverOption,
        HttpClient? http,
        TextWriter error,
        Func<ServerClient, string, Uri, Task<int>> action
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        var agent = AgentName.Read(dir, error);
        return agent is null ? 2 : await RunAsync(serverOption, http, error, (client, site) => action(client, agent, site));
    }
}
