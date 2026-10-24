namespace Suwageval.Cli.Server;

/// <summary>
/// The server could not be reached or refused a request; the message is for the person at the terminal. <see cref="Feature"/> names the
/// area (<c>evaluation</c>, <c>monitoring</c>) when the refusal was that the agent's <c>features</c> switch it off.
/// </summary>
internal sealed class ServerClientException(string message, string? feature = null) : Exception(message)
{
    public string? Feature { get; } = feature;
}
