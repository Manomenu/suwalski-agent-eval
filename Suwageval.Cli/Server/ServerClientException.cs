namespace Suwageval.Cli.Server;

internal sealed class ServerClientException(string message, string? feature = null) : Exception(message)
{
    public string? Feature { get; } = feature;
}
