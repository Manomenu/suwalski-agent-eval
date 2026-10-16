namespace Suwageval.Cli.Server;

/// <summary>
/// Where Suwageval is: the web app's address, the one a browser opens. The API is under its <c>/api</c>, the way the browser reaches
/// it (nginx in the image, Vite's proxy on a laptop), so one address serves the commands and the links they print.
/// </summary>
internal static class ServerAddress
{
    public const string Variable = "SUWAGEVAL_URL";
    public const string Default = "http://localhost:3230";

    public static string OptionDescription =>
        $"Suwageval's address (the web app; the API is under /api). Default: ${Variable}, else {Default}.";

    /// <summary>From <c>--server</c>, else <see cref="Variable"/>, else <see cref="Default"/>; with a trailing slash, so relative paths stay under it.</summary>
    public static Uri Site(string? option)
    {
        var address = option ?? Environment.GetEnvironmentVariable(Variable);
        return new Uri((string.IsNullOrWhiteSpace(address) ? Default : address).TrimEnd('/') + "/");
    }

    public static Uri Api(Uri site) => new(site, "api/");

    /// <summary>The web page of a run.</summary>
    public static Uri RunPage(Uri site, Guid runId) => new(site, $"runs/{runId}");
}
