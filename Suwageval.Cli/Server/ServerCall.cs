using Suwageval.Cli.Files;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Server;

internal static class ServerCall
{
    public static async Task<int> RunAsync(
        string? serverOption,
        HttpClient? http,
        TextWriter error,
        Func<ServerClient, Uri, Task<int>> action,
        int failureExit = 2,
        string agentDir = AgentFolder.DefaultDir
    )
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(action);
        var site = ServerAddress.Site(serverOption, agentDir);
        HttpClient? owned = null;
        try
        {
            return await action(new ServerClient(http ?? (owned = new HttpClient { BaseAddress = ServerAddress.Api(site) })), site);
        }
        catch (ServerClientException failure)
        {
            await error.WriteLineAsync(failure.Message);
            // Why: the server has the last push, so when the local file switches the refused area on the push is what is missing
            if (failure.Feature is { } feature && AgentReader.Quietly(agentDir)?.Definition is { } local && SwitchesOn(local, feature))
            {
                await error.WriteLineAsync($"{Path.Combine(agentDir, "agent.yaml")} switches {feature} on: `suwageval push` to apply it.");
            }
            return failureExit;
        }
        finally
        {
            owned?.Dispose();
        }
    }

    public static async Task<int> ForAgentAsync(
        string dir,
        string? serverOption,
        HttpClient? http,
        TextWriter error,
        Func<ServerClient, string, Uri, Task<int>> action
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        var agent = AgentReader.Read(dir, error);
        return agent is null
            ? 2
            : await RunAsync(serverOption, http, error, (client, site) => action(client, agent.Definition.Name, site), agentDir: dir);
    }

    private static bool SwitchesOn(AgentDefinition definition, string feature) =>
        feature switch
        {
            "evaluation" => definition.Features?.Evaluation ?? true,
            "monitoring" => definition.Features?.Monitoring is not null,
            _ => false,
        };
}
