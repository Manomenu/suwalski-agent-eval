using System.Text.RegularExpressions;
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
/// The server is cut into AREAS (<c>Evaluation</c>, later <c>Monitoring</c> beside it): an area groups the features of one product
/// area and has an inner order of its own (<see cref="Areas"/>). The top order puts the areas above the SHARED features
/// (<c>Judges</c>, <c>Verdicts</c>, <c>Agents</c>, <c>Database</c>, …), so a shared feature never uses an area, and areas on one level
/// never use each other.
/// EVERY namespace of Suwageval.Server has a place in <see cref="Layers"/> (or in its area's order) — the last tests fail the gate
/// when one is missing. A new shared feature <c>Reports</c> that stores in the database:
/// <code>
/// "Api | LiveCheck",
/// "Evaluation",
/// "Reports",
/// "Database | Problems",
/// </code>
/// A new feature of the area goes into <c>Areas["Evaluation"]</c>. A feature with an inner order of its own (endpoints &gt; store &gt; model)
/// gets a test like <see cref="Layers_use_only_the_layers_below_them"/> over its sub-namespaces. A sub-feature (a folder inside a
/// feature, <c>Evaluation/Runs/Groups/</c>) needs no entry: it shares its feature's layer, and
/// <see cref="Sub_features_are_used_by_neither_their_feature_nor_their_siblings"/> keeps it on top of that feature.
/// </remarks>
public sealed class ContractsTests
{
    private const string Root = "Suwageval.Server";

    /// <summary>Top to bottom. An area is one entry here; its features are ordered in <see cref="Areas"/>.</summary>
    private static readonly string[] Layers =
    [
        "Api",
        "LiveCheck | ScenarioDrafts",
        "Evaluation | Monitoring",
        "Judges",
        "Verdicts | LanguageModels",
        "Agents",
        "Database | Problems",
    ];

    /// <summary>The areas of the server (they appear in <see cref="Layers"/>) with the order of their features, top to bottom.</summary>
    private static readonly Dictionary<string, string[]> Areas = new()
    {
        ["Evaluation"] = ["Runs", "Simulation", "Checks | Invocation", "Scenarios"],
        ["Monitoring"] = ["Conversations"],
    };

    private static readonly Architecture Server = new ArchLoader().LoadAssemblies(typeof(ServerApp).Assembly).Build();

    /// <summary>Every pair (part, part it must not use): the parts above it and its siblings, named under <paramref name="prefix"/>.</summary>
    public static IEnumerable<(string Part, string Forbidden)> EdgesOf(string[] layers, string prefix = "")
    {
        var levels = layers.Select(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToArray();
        for (var level = 0; level < levels.Length; level++)
        {
            foreach (var part in levels[level])
            {
                // Everything above, and the siblings on the same level.
                foreach (
                    var forbidden in levels.Take(level).SelectMany(above => above).Concat(levels[level].Where(sibling => sibling != part))
                )
                {
                    yield return (prefix + part, prefix + forbidden);
                }
            }
        }
    }

    public static TheoryData<string, string> Edges()
    {
        var data = new TheoryData<string, string>();
        foreach (var (part, forbidden) in EdgesOf(Layers))
        {
            data.Add(part, forbidden);
        }
        foreach (var (area, features) in Areas)
        {
            foreach (var (part, forbidden) in EdgesOf(features, area + "."))
            {
                data.Add(part, forbidden);
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

    /// <summary>The rules report what they are for: an area never uses another area, a shared feature never uses an area.</summary>
    [Fact]
    public void Edges_forbid_areas_using_each_other_and_shared_features_using_areas()
    {
        var edges = EdgesOf(["Api", "Evaluation | Monitoring", "Judges", "Database"]).ToHashSet();

        Assert.Contains(("Evaluation", "Monitoring"), edges);
        Assert.Contains(("Monitoring", "Evaluation"), edges);
        Assert.Contains(("Judges", "Evaluation"), edges);
        Assert.Contains(("Judges", "Monitoring"), edges);
        Assert.Contains(("Database", "Monitoring"), edges);
        Assert.DoesNotContain(("Evaluation", "Judges"), edges);
    }

    [Fact]
    public void Every_server_namespace_has_a_layer()
    {
        var layered = Layers.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet();
        Assert.True(Areas.Keys.All(layered.Contains), "every key of Areas is an entry of Layers");
        var inAreas = Areas.ToDictionary(
            area => area.Key,
            area => area.Value.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet()
        );
        var missing = Server
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.'))
            .Where(segments =>
                !layered.Contains(segments[0])
                || (inAreas.TryGetValue(segments[0], out var features) && segments.Length > 1 && !features.Contains(segments[1]))
            )
            .Select(segments => string.Join('.', segments.Take(inAreas.ContainsKey(segments[0]) ? 2 : 1)))
            .Distinct()
            .Order()
            .ToArray();

        Assert.True(missing.Length == 0, $"add to Layers (or Areas) in ContractsTests.cs: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Every <c>Root.Feature.Sub</c> namespace (<c>Root.Area.Feature.Sub</c> in an area) with the namespaces that must not use it: the
    /// feature's own and its other sub-features (AGENTS.md, section 5: a sub-feature is built on its feature, never the other way round).
    /// </summary>
    public static TheoryData<string, string> SubFeatureEdges()
    {
        var data = new TheoryData<string, string>();
        var subFeatures = Server
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.'))
            .Select(segments => (Segments: segments, FeatureDepth: Areas.ContainsKey(segments[0]) ? 2 : 1))
            .Where(namespaceInfo => namespaceInfo.Segments.Length > namespaceInfo.FeatureDepth)
            .Select(namespaceInfo =>
                (
                    Feature: string.Join('.', namespaceInfo.Segments.Take(namespaceInfo.FeatureDepth)),
                    Sub: namespaceInfo.Segments[namespaceInfo.FeatureDepth]
                )
            )
            .Distinct()
            .ToArray();
        foreach (var (feature, sub) in subFeatures)
        {
            var subPattern = $@"^{Regex.Escape(Root + "." + feature)}\.{sub}(\..+)?$";
            data.Add(subPattern, $"^{Regex.Escape(Root + "." + feature)}$");
            foreach (var (_, sibling) in subFeatures.Where(other => other.Feature == feature && other.Sub != sub))
            {
                data.Add(subPattern, $@"^{Regex.Escape(Root + "." + feature)}\.{sibling}(\..+)?$");
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

    private static string Pattern(string part) => $@"^{Regex.Escape(Root + "." + part)}(\..+)?$";
}
