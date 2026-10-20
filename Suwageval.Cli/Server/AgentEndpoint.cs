namespace Suwageval.Cli.Server;

internal static class AgentEndpoint
{
    public const string Variable = "SUWAGEVAL_ENDPOINT_URL";

    public static string OptionDescription(string fallback) => $"Reach the agent here. Default: ${Variable}, else {fallback}";

    /// <summary>From <c>--endpoint</c>, else <see cref="Variable"/>; null when neither says anything, so the caller falls back.</summary>
    public static string? Override(string? option) => Override(option, Environment.GetEnvironmentVariable(Variable));

    public static string? Override(string? option, string? variable) =>
        !string.IsNullOrWhiteSpace(option) ? option
        : !string.IsNullOrWhiteSpace(variable) ? variable
        : null;
}
