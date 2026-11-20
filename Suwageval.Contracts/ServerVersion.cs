using System.Reflection;

namespace Suwageval.Contracts;

public static class ServerVersion
{
    public const string Header = "Suwageval-Version";

    public static string Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
