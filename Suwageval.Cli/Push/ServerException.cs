namespace Suwageval.Cli.Push;

/// <summary>The server could not be reached, or answered with an error; the message is for the person running the command.</summary>
internal sealed class ServerException(string message) : Exception(message);
