using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Suwageval.AgentSdk;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Suwageval.Server.Tests;

/// <summary>
/// The import contracts of <c>Suwageval.AgentSdk</c> (AGENTS.md, section 4), the same rule as the server's. <c>"."</c> is the
/// root namespace (<c>ContractJson</c>). EVERY namespace of the SDK has a place in <see cref="Layers"/>. It has no test project of
/// its own, so its tests live here.
/// </summary>
public sealed class AgentSdkContractsTests
{
    private const string Root = "Suwageval.AgentSdk";

    /// <summary>Top to bottom: the endpoints use the contract and the log items, which use only the JSON options.</summary>
    private static readonly string[] Layers = ["Invocation", "Agents | Conversations", "."];

    private static readonly Architecture Sdk = new ArchLoader().LoadAssemblies(typeof(ContractJson).Assembly).Build();

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
            .Check(Sdk);

    [Fact]
    public void Every_sdk_namespace_has_a_layer()
    {
        var layered = Layers.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet();
        var parts = Sdk
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.')[0])
            .ToHashSet();

        Assert.True(parts.IsSubsetOf(layered), $"add to Layers in ContractsTests.cs: {string.Join(", ", parts.Except(layered).Order())}");
    }

    private static string Pattern(string part) => part == "." ? $"^{Root}$" : $@"^{Root}\.{part}(\..+)?$";
}
