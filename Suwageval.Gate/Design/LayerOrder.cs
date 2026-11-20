namespace Suwageval.Gate.Design;

internal sealed record LayerOrder(string[] Layers, Dictionary<string, string[]> Areas)
{
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

    public TheoryData<string, string> AreaEdges() => Data(AreaEdgePairs());

    public TheoryData<string, string> AllEdges() => Data(EdgesOf(Layers).Concat(AreaEdgePairs()));

    public string[] UnplacedInAreas(IEnumerable<string> namespaces, string root) =>
        [.. Unplaced(namespaces, root).Where(name => name.StartsWith("area ", StringComparison.Ordinal) || name.Contains('.'))];

    public string[] Unplaced(IEnumerable<string> namespaces, string root)
    {
        var layered = Layers.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet();
        var inAreas = Areas.ToDictionary(
            area => area.Key,
            area => area.Value.SelectMany(layer => layer.Split('|', StringSplitOptions.TrimEntries)).ToHashSet()
        );
        return
        [
            .. Areas.Keys.Where(area => !layered.Contains(area)).Select(area => $"area {area} has no layer"),
            .. namespaces
                .Where(name => name.StartsWith(root + ".", StringComparison.Ordinal))
                .Select(name => name[(root.Length + 1)..].Split('.'))
                .Where(segments =>
                    !layered.Contains(segments[0])
                    || (inAreas.TryGetValue(segments[0], out var features) && segments.Length > 1 && !features.Contains(segments[1]))
                )
                .Select(segments => string.Join('.', segments.Take(inAreas.ContainsKey(segments[0]) ? 2 : 1)))
                .Distinct()
                .Order(),
        ];
    }

    private IEnumerable<(string Part, string Forbidden)> AreaEdgePairs() => Areas.SelectMany(area => EdgesOf(area.Value, area.Key + "."));

    private static TheoryData<string, string> Data(IEnumerable<(string Part, string Forbidden)> edges)
    {
        var data = new TheoryData<string, string>();
        foreach (var (part, forbidden) in edges)
        {
            data.Add(part, forbidden);
        }
        return data;
    }
}
