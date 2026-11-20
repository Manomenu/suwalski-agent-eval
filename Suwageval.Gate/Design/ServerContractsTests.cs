using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Suwageval.Gate.Design;

public sealed class ServerContractsTests
{
    private const string Root = "Suwageval.Server";

    private static readonly Architecture Server = new ArchLoader().LoadAssemblies(GuardTests.Server).Build();

    public static TheoryData<string, string> Edges() => LayerOrders.Server.AreaEdges();

    [Theory]
    [MemberData(nameof(Edges))]
    public void Features_of_an_area_use_only_the_features_below_them(string part, string forbidden) =>
        Types()
            .That()
            .ResideInNamespaceMatching(Pattern(part))
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(Pattern(forbidden)))
            .Because($"{part} must not use {forbidden}: move the shared code down or pass it in from above ({LayerOrders.Path})")
            .WithoutRequiringPositiveResults()
            .Check(Server);

    [Fact]
    public void Edges_forbid_areas_using_each_other_and_shared_features_using_areas()
    {
        var edges = LayerOrder.EdgesOf(["Api", "Evaluation | Monitoring", "Judges", "Database"]).ToHashSet();

        Assert.Contains(("Evaluation", "Monitoring"), edges);
        Assert.Contains(("Monitoring", "Evaluation"), edges);
        Assert.Contains(("Judges", "Evaluation"), edges);
        Assert.Contains(("Judges", "Monitoring"), edges);
        Assert.Contains(("Database", "Monitoring"), edges);
        Assert.DoesNotContain(("Evaluation", "Judges"), edges);
    }

    [Fact]
    public void Every_namespace_of_an_area_has_a_feature()
    {
        var missing = LayerOrders.Server.UnplacedInAreas(Server.Namespaces.Select(ns => ns.FullName), Root);

        Assert.True(
            missing.Length == 0,
            $"add to the areas of {Root} in {LayerOrders.Path}, at the place it belongs: {string.Join(", ", missing)}"
        );
    }

    public static TheoryData<string, string> SubFeatureEdges()
    {
        var areas = LayerOrders.Server.Areas;
        var data = new TheoryData<string, string>();
        var subFeatures = Server
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.'))
            .Select(segments => (Segments: segments, FeatureDepth: areas.ContainsKey(segments[0]) ? 2 : 1))
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
            .Because("a sub-feature is built on its feature: what the feature needs moves up into the feature")
            .WithoutRequiringPositiveResults()
            .Check(Server);

    private static string Pattern(string part) => $@"^{Regex.Escape(Root + "." + part)}(\..+)?$";
}
