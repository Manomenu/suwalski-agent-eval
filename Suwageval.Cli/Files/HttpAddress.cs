namespace Suwageval.Cli.Files;

internal static class HttpAddress
{
    public static string? Problem(string field, string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? null
            : $"{field} is an absolute http or https address (like http://localhost:3230).";
}
