using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Suwageval.Server.Tests;

/// <summary>
/// Where the CLI meets the server (AGENTS.md, section 4): only the end-to-end tests may use the CLI, and nothing else
/// may use them. Read from the compiled assemblies, like <see cref="ContractsTests"/>.
/// </summary>
public sealed class EndToEndContractsTests
{
    private const string EndToEnd = @"^Suwageval\.Server\.Tests\.EndToEnd(\..+)?$";

    // By name, not typeof: this file is itself outside EndToEnd and must not use the CLI.
    private static readonly Assembly CliAssembly = Assembly.Load("suwageval");

    private static readonly Architecture Tests = new ArchLoader()
        .LoadAssemblies(typeof(EndToEndContractsTests).Assembly, CliAssembly)
        .Build();

    [Fact]
    public void Only_the_end_to_end_tests_use_the_cli() =>
        Types()
            .That()
            .ResideInAssembly(typeof(EndToEndContractsTests).Assembly)
            .And()
            .DoNotResideInNamespaceMatching(EndToEnd)
            .Should()
            .NotDependOnAny(Types().That().ResideInAssembly(CliAssembly))
            .Check(Tests);

    [Fact]
    public void Nothing_but_the_end_to_end_tests_uses_them() =>
        Types()
            .That()
            .ResideInAssembly(typeof(EndToEndContractsTests).Assembly)
            .And()
            .DoNotResideInNamespaceMatching(EndToEnd)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(EndToEnd))
            .Check(Tests);
}
