using System.Reflection;

namespace Suwageval.Contracts;

/// <summary>
/// The build of the server, sent with every answer, so the CLI can tell that it is older than the server when it cannot read an
/// answer (a field or a value it does not know yet).
/// </summary>
public static class ServerVersion
{
    /// <summary>The response header that carries the server's build.</summary>
    public const string Header = "Suwageval-Version";

    /// <summary>The build of <paramref name="assembly"/>: its informational version (the version and the commit), or its version.</summary>
    public static string Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
