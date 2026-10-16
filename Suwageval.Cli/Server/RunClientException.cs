namespace Suwageval.Cli.Server;

/// <summary>The server could not be reached or refused a request; the message is for the person at the terminal.</summary>
internal sealed class RunClientException(string message) : Exception(message);
