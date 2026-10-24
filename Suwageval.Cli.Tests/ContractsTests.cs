using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Suwageval.Cli.Tests;

/// <summary>
/// The CLI's import contracts (AGENTS.md, section 4), the same rule as the server's: a layer may use the layers below
/// it, never the ones above; parts on one level (<c>"A | B"</c>) must not use each other. <c>"."</c> is the root
/// namespace (<c>Program</c>, <c>CliApp</c>). The commands are cut into AREAS (<c>Evaluation</c>, later <c>Monitoring</c> beside it):
/// an area is one entry of <see cref="Layers"/>, level with the shared commands (<c>Init</c>, <c>Agents</c>, …), so an area and a
/// shared command never use each other; the commands inside an area are ordered in <see cref="Areas"/> (siblings, none uses another).
/// The command NAMES stay flat (<c>suwageval run</c>); only the code and the help follow the areas. EVERY namespace of the CLI has a
/// place in <see cref="Layers"/> (or in its area's order). A new command of the area goes into <c>Areas["Evaluation"]</c>.
/// </summary>
public sealed class ContractsTests
{
    private const string Root = "Suwageval.Cli";

    /// <summary>Top to bottom. An area is one entry here; its commands are ordered in <see cref="Areas"/>. What two commands share goes into a layer below.</summary>
    private static readonly string[] Layers =
    [
        ".",
        "Evaluation | Monitoring | Init | Agents | Versions | AgentDelete",
        "Output",
        "Validation | Server",
        "Files",
    ];

    /// <summary>The areas of the CLI (they appear in <see cref="Layers"/>) with the order of their commands, top to bottom.</summary>
    private static readonly Dictionary<string, string[]> Areas = new()
    {
        ["Evaluation"] =
        [
            "Validate | Push | Run | Rerun | Recheck | Cancel | Results | Compare | Sweep | Runs | Groups | Note | Scenarios | Trend | Unstable | AgentErrors",
        ],
        ["Monitoring"] = ["Conversations | Checks"],
    };

    private static readonly Architecture Cli = new ArchLoader().LoadAssemblies(typeof(CliApp).Assembly).Build();

    /// <summary>Every pair (part, part it must not use): the parts above it and its siblings, named under <paramref name="prefix"/>.</summary>
    public static IEnumerable<(string Part, string Forbidden)> EdgesOf(string[] layers, string prefix = "")
    {
        var levels = layers.Select(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToArray();
        for (var level = 0; level < levels.Length; level++)
        {
            foreach (var part in levels[level])
            {
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
        foreach (var (area, commands) in Areas)
        {
            foreach (var (part, forbidden) in EdgesOf(commands, area + "."))
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
            .Check(Cli);

    /// <summary>The rules report what they are for: an area and a shared command never use each other, nor do two areas.</summary>
    [Fact]
    public void Edges_forbid_areas_and_shared_commands_using_each_other()
    {
        var edges = EdgesOf([".", "Evaluation | Monitoring | Init", "Output"]).ToHashSet();

        Assert.Contains(("Evaluation", "Init"), edges);
        Assert.Contains(("Init", "Evaluation"), edges);
        Assert.Contains(("Evaluation", "Monitoring"), edges);
        Assert.Contains(("Output", "Evaluation"), edges);
        Assert.DoesNotContain(("Evaluation", "Output"), edges);
    }

    [Fact]
    public void Every_cli_namespace_has_a_layer()
    {
        var layered = Layers.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet();
        Assert.True(Areas.Keys.All(layered.Contains), "every key of Areas is an entry of Layers");
        var inAreas = Areas.ToDictionary(
            area => area.Key,
            area => area.Value.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet()
        );
        var missing = Cli
            .Namespaces.Select(ns => ns.FullName)
            .Where(name => name.StartsWith(Root + ".", StringComparison.Ordinal))
            .Select(name => name[(Root.Length + 1)..].Split('.'))
            .Where(segments =>
                !layered.Contains(segments[0])
                || (inAreas.TryGetValue(segments[0], out var commands) && segments.Length > 1 && !commands.Contains(segments[1]))
            )
            .Select(segments => string.Join('.', segments.Take(inAreas.ContainsKey(segments[0]) ? 2 : 1)))
            .Distinct()
            .Order()
            .ToArray();

        Assert.True(missing.Length == 0, $"add to Layers (or Areas) in ContractsTests.cs: {string.Join(", ", missing)}");
    }

    private static string Pattern(string part) => part == "." ? $"^{Root}$" : $@"^{Root}\.{part}(\..+)?$";
}
