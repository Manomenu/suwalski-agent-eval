using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Suwageval.Server.Api;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Suwageval.Server.Tests;

/// <summary>
/// The server's import contracts (AGENTS.md, section 4): which part may use which. A layer may use the
/// layers below it, never the ones above; parts on one level (<c>"A | B"</c>) must not use each other.
/// </summary>
/// <remarks>
/// EVERY namespace of Suwageval.Server has a place in <see cref="Layers"/> — the last test fails the gate
/// when one is missing. A new feature therefore lands here in the same change, e.g. for a feature
/// <c>Scenarios</c> that stores in the database and a feature <c>Evaluations</c> built on top of it:
/// <code>
/// "Api",
/// "Evaluations",
/// "Scenarios",
/// "Database",
/// </code>
/// A feature with an inner order of its own (endpoints &gt; store &gt; model) gets a test like
/// <see cref="Layers_use_only_the_layers_below_them"/> over its sub-namespaces. A sub-feature (a folder inside a
/// feature, <c>Runs/Groups/</c>) needs no entry: it shares its feature's layer, and
/// <see cref="Sub_features_are_used_by_neither_their_feature_nor_their_siblings"/> keeps it on top of that feature.
/// </remarks>
public sealed class ContractsTests
{
    private const string Root = "Suwageval.Server";

    /// <summary>Top to bottom.</summary>
    private static readonly string[] Layers =
    [
        "Api",
        "Runs",
        "Judges",
        "Simulation",
        "Checks | Invocation | Models",
        "Scenarios",
        "Agents",
        "Database | Problems",
    ];

    private static readonly Architecture Server = new ArchLoader().LoadAssemblies(typeof(ServerApp).Assembly).Build();

    public static TheoryData<string, string> Edges()
    {
        var data = new TheoryData<string, string>();
        var levels = Layers.Select(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToArray();
        for (var level = 0; level < levels.Length; level++)
        {
            foreach (var part in levels[level])
            {
                // Everything above, and the siblings on the same level.
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
            .Check(Server);

    [Fact]
    public void Every_server_namespace_has_a_layer()
    {
        var layered = Layers.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet();
        var parts = Server
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.')[0])
            .ToHashSet();

        Assert.True(parts.IsSubsetOf(layered), $"add to Layers in ContractsTests.cs: {string.Join(", ", parts.Except(layered).Order())}");
    }

    /// <summary>
    /// Every <c>Root.Feature.Sub</c> namespace with the namespaces that must not use it: the feature's own (<c>Root.Feature</c>) and
    /// its other sub-features (AGENTS.md, section 5: a sub-feature is built on its feature, never the other way round).
    /// </summary>
    public static TheoryData<string, string> SubFeatureEdges()
    {
        var data = new TheoryData<string, string>();
        var subFeatures = Server
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.'))
            .Where(segments => segments.Length >= 2)
            .Select(segments => (Feature: segments[0], Sub: segments[1]))
            .Distinct()
            .ToArray();
        foreach (var (feature, sub) in subFeatures)
        {
            data.Add($@"^{Root}\.{feature}\.{sub}(\..+)?$", $@"^{Root}\.{feature}$");
            foreach (var (_, sibling) in subFeatures.Where(other => other.Feature == feature && other.Sub != sub))
            {
                data.Add($@"^{Root}\.{feature}\.{sub}(\..+)?$", $@"^{Root}\.{feature}\.{sibling}(\..+)?$");
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SubFeatureEdges))]
    public void Sub_features_are_used_by_neither_their_feature_nor_their_siblings(string subFeature, string user) =>
        Types()
            .That()
            .ResideInNamespaceMatching(user)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(subFeature))
            .WithoutRequiringPositiveResults()
            .Check(Server);

    /// <summary>
    /// A second set of routes in one folder is a second feature in it (AGENTS.md, section 5): it moves to a folder of its own,
    /// beside the feature or inside it as a sub-feature.
    /// </summary>
    [Fact]
    public void Every_namespace_has_at_most_one_endpoints_class()
    {
        var crowded = typeof(ServerApp)
            .Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Endpoints", StringComparison.Ordinal))
            .GroupBy(type => type.Namespace)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(type => type.Name).Order())}")
            .ToArray();

        Assert.True(crowded.Length == 0, $"give each its own folder: {string.Join("; ", crowded)}");
    }

    private static string Pattern(string part) => $@"^{Root}\.{part}(\..+)?$";
}
