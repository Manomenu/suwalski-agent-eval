using System.Reflection;
using System.Runtime.CompilerServices;

namespace Suwageval.Gate.Design;

internal static class SolutionTypes
{
    private static readonly Assembly[] Assemblies =
    [
        ServerContractsTests.ServerAssembly,
        CliContractsTests.CliAssembly,
        Assembly.Load("Suwageval.Contracts"),
        Assembly.Load("Suwageval.AgentSdk"),
        typeof(SolutionTypes).Assembly,
    ];

    public static IReadOnlyList<Type> All { get; } =
    [
        .. Assemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Select(info => info.AsType())
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Where(type => !(type.FullName ?? "").Contains('<', StringComparison.Ordinal)),
    ];

    public static string SimpleName(Type type) => type.Name.Split('`')[0];
}
