using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Suwageval.Cli.Tests;

/// <summary>
/// The CLI's import contracts (AGENTS.md, section 4), the same rule as the server's: a layer may use the layers below
/// it, never the ones above; parts on one level (<c>"A | B"</c>) must not use each other. <c>"."</c> is the root
/// namespace (<c>Program</c>, <c>CliApp</c>). EVERY namespace of the CLI has a place in <see cref="Layers"/>.
/// </summary>
public sealed class ContractsTests
{
    private const string Root = "Suwageval.Cli";

    /// <summary>Top to bottom. The commands are siblings; what two of them share goes into a layer below.</summary>
    private static readonly string[] Layers =
    [
        ".",
        "Validate | Push | Run | Rerun | Cancel | Runs | Unstable | Results | Compare | Versions",
        "Validation | Server",
        "Files",
    ];

    private static readonly Architecture Cli = new ArchLoader().LoadAssemblies(typeof(CliApp).Assembly).Build();

    public static TheoryData<string, string> Edges()
    {
        var data = new TheoryData<string, string>();
        var levels = Layers.Select(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToArray();
        for (var level = 0; level < levels.Length; level++)
        {
            foreach (var part in levels[level])
            {
                foreach (
                    var forbidden in levels.Take(level).SelectMany(above => above).Concat(levels[level].Where(sibling => sibling != part))
                )
                {
                    data.Add(part, forbidden);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void Layers_use_only_the_layers_below_them(string part, string forbidden) =>
        Types()
            .That()
            .ResideInNamespaceMatching(Pattern(part))
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(Pattern(forbidden)))
            .WithoutRequiringPositiveResults()
            .Check(Cli);

    [Fact]
    public void Every_cli_namespace_has_a_layer()
    {
        var layered = Layers.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet();
        var parts = Cli
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.')[0])
            .ToHashSet();

        Assert.True(parts.IsSubsetOf(layered), $"add to Layers in ContractsTests.cs: {string.Join(", ", parts.Except(layered).Order())}");
    }

    private static string Pattern(string part) => part == "." ? $"^{Root}$" : $@"^{Root}\.{part}(\..+)?$";
}
