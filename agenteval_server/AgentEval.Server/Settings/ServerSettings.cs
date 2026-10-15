using System.Globalization;
using System.Text.Json;

namespace AgentEval.Server.Settings;

/// <summary>
/// Everything the server reads from its environment. Each setting has a working local default, so
/// the server starts on a laptop with no .env at all; compose and the cluster override what differs.
/// </summary>
/// <param name="Host">Inside a container the pod network is the boundary, so it listens on every interface.</param>
/// <param name="Port">The port the API listens on.</param>
/// <param name="CorsOrigins">
/// Browser origins allowed to call the API directly. Behind the vite proxy and nginx the browser sees a
/// single origin, so this only matters for a UI served from elsewhere.
/// </param>
/// <param name="DatabaseUrl">
/// A <c>postgresql://</c> URL. The default is the container <c>just db up</c> starts; compose and the
/// cluster point it at their own database.
/// </param>
internal sealed record ServerSettings(string Host, int Port, IReadOnlyList<string> CorsOrigins, string DatabaseUrl)
{
    public static ServerSettings Defaults { get; } =
        new("0.0.0.0", 6230, ["http://localhost:3230"], "postgresql://agenteval:agenteval@localhost:5463/agenteval");

    /// <summary>The settings from configuration keys named like the environment variables (<c>DATABASE_URL</c>).</summary>
    public static ServerSettings From(IConfiguration config) =>
        new(
            config["HOST"] ?? Defaults.Host,
            config["PORT"] is { } port ? ParsePort(port) : Defaults.Port,
            config["CORS_ORIGINS"] is { } origins ? ParseList("CORS_ORIGINS", origins) : Defaults.CorsOrigins,
            config["DATABASE_URL"] ?? Defaults.DatabaseUrl
        );

    private static int ParsePort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536
            ? port
            : throw new FormatException($"PORT must be a port number, got \"{value}\"");

    private static string[] ParseList(string name, string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(value) ?? throw new FormatException($"{name} must be a JSON list, got null");
        }
        catch (JsonException error)
        {
            throw new FormatException($"{name} must be a JSON list of strings, got {value}", error);
        }
    }
}
