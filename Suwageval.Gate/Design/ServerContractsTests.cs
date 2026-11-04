using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Suwageval.Gate.Design;

public sealed class ServerContractsTests
{
    private const string Root = "Suwageval.Server";

    // Why: by name, not typeof: ServerApp is internal and only the server's tests may see its internals
    internal static readonly Assembly ServerAssembly = Assembly.Load(Root);

    internal static readonly Architecture Server = new ArchLoader().LoadAssemblies(ServerAssembly).Build();

    public static TheoryData<string, string> Edges() => LayersFile.Server.Edges();

    [Theory]
    [MemberData(nameof(Edges))]
    public void Layers_use_only_the_layers_below_them(string part, string forbidden) =>
        Types()
            .That()
            .ResideInNamespaceMatching(Pattern(part))
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(Pattern(forbidden)))
            .Because($"{part} must not use {forbidden}: move the shared code down or pass it in from above ({LayersFile.Path})")
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
    public void Every_server_namespace_has_a_layer()
    {
        var missing = LayersFile.Server.Unplaced(Server.Namespaces.Select(ns => ns.FullName), Root);

        Assert.True(
            missing.Length == 0,
            $"add to server.layers (or server.areas) in {LayersFile.Path}, at the place it belongs: {string.Join(", ", missing)}"
        );
    }

    public static TheoryData<string, string> SubFeatureEdges()
    {
        var areas = LayersFile.Server.Areas;
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

    [Fact]
    public void Every_namespace_has_at_most_one_endpoints_class()
    {
        var crowded = ServerAssembly
            .DefinedTypes.Where(type => type.Name.EndsWith("Endpoints", StringComparison.Ordinal))
            .GroupBy(type => type.Namespace)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(type => type.Name).Order())}")
            .ToArray();

        Assert.True(
            crowded.Length == 0,
            $"a second set of routes is a second feature, give each its own folder: {string.Join("; ", crowded)}"
        );
    }

    private static string Pattern(string part) => $@"^{Regex.Escape(Root + "." + part)}(\..+)?$";
}
