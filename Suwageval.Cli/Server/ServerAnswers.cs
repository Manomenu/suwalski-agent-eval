using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts;

namespace Suwageval.Cli.Server;

/// <summary>How the CLI reads the server's answers, and what it says when it cannot.</summary>
internal static class ServerAnswers
{
    /// <summary>
    /// <see cref="ContractJson.Options"/> except that a field the CLI does not know is skipped: a newer server may add one, and an
    /// installed CLI should keep working. Files a person writes are still read strictly (a typo there must not pass).
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>The build of this CLI, compared with the server's when an answer cannot be read.</summary>
    public static string CliVersion { get; } = ServerVersion.Of(typeof(ServerAnswers).Assembly);

    /// <summary>
    /// Why an answer could not be read; when the server is another build (or did not say which), most likely this CLI is older than
    /// the server, and how to update it.
    /// </summary>
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
