using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Suwageval.Gate.Design;

public sealed class CliContractsTests
{
    private const string Root = "Suwageval.Cli";

    internal static readonly Assembly CliAssembly = Assembly.Load("suwageval");

    internal static readonly Architecture Cli = new ArchLoader().LoadAssemblies(CliAssembly).Build();

    public static TheoryData<string, string> Edges() => LayersFile.Cli.Edges();

    [Theory]
    [MemberData(nameof(Edges))]
    public void Layers_use_only_the_layers_below_them(string part, string forbidden) =>
        Types()
            .That()
            .ResideInNamespaceMatching(Pattern(part))
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(Pattern(forbidden)))
            .Because($"{part} must not use {forbidden}: what two commands share moves down into a layer ({LayersFile.Path})")
            .WithoutRequiringPositiveResults()
            .Check(Cli);

    [Fact]
    public void Edges_forbid_areas_and_shared_commands_using_each_other()
    {
        var edges = LayerOrder.EdgesOf([".", "Evaluation | Monitoring | Init", "Output"]).ToHashSet();

        Assert.Contains(("Evaluation", "Init"), edges);
        Assert.Contains(("Init", "Evaluation"), edges);
        Assert.Contains(("Evaluation", "Monitoring"), edges);
        Assert.Contains(("Output", "Evaluation"), edges);
        Assert.DoesNotContain(("Evaluation", "Output"), edges);
    }

    [Fact]
    public void Every_cli_namespace_has_a_layer()
    {
        var missing = LayersFile.Cli.Unplaced(Cli.Namespaces.Select(ns => ns.FullName), Root);

        Assert.True(
            missing.Length == 0,
            $"add to cli.layers (or cli.areas) in {LayersFile.Path}, at the place it belongs: {string.Join(", ", missing)}"
        );
    }

    private static string Pattern(string part) => part == "." ? $"^{Root}$" : $@"^{Regex.Escape(Root + "." + part)}(\..+)?$";
}
