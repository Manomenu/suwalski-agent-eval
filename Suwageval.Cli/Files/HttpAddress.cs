namespace Suwageval.Cli.Files;

/// <summary>The rule for an address written in <c>agent.yaml</c> (<c>endpointUrl</c>, <c>suwagevalUrl</c>), shared by the reader and <c>init</c>.</summary>
internal static class HttpAddress
{
    /// <summary>What is wrong with <paramref name="value"/> as the value of <paramref name="field"/>, or null.</summary>
    public static string? Problem(string field, string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? null
            : $"{field} is an absolute http or https address (like http://localhost:3230).";
}
