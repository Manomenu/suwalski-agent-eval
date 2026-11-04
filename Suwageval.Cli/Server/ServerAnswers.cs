using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts;

namespace Suwageval.Cli.Server;

internal static class ServerAnswers
{
    // Why: a newer server may add fields, and an installed CLI must keep working; files a person writes stay strict so a typo does not pass
    public static JsonSerializerOptions Options { get; } = Create();

    public static string CliVersion { get; } = ServerVersion.Of(typeof(ServerAnswers).Assembly);

    public static string Unreadable(string error, string? serverVersion)
    {
        var text = $"The server's answer could not be read: {error}";
        return serverVersion == CliVersion
            ? text
            : $"{text}\nThis suwageval ({CliVersion}) is probably older than the server ({serverVersion ?? "build unknown"}): update it — "
                + "run `just cli-install` in the suwageval repository.";
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(ContractJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
