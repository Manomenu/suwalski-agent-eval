using System.Text.Json;

namespace Suwageval.Gate.Design;

internal static class LayersFile
{
    public const string Path = "Suwageval.Gate/layers.json";

    private static readonly Lazy<JsonDocument> Document = new(() => JsonDocument.Parse(RepositoryFiles.Read(Path)));

    public static LayerOrder Server => Order("server");

    public static LayerOrder Cli => Order("cli");

    public static Dictionary<string, (string[] References, string[] InternalsVisibleTo)> Projects() =>
        Document
            .Value.RootElement.GetProperty("projects")
            .EnumerateObject()
            .ToDictionary(
                project => project.Name,
                project => (Strings(project.Value.GetProperty("references")), Strings(project.Value.GetProperty("internalsVisibleTo")))
            );

    private static LayerOrder Order(string name)
    {
        var order = Document.Value.RootElement.GetProperty(name);
        return new LayerOrder(
            Strings(order.GetProperty("layers")),
            order.GetProperty("areas").EnumerateObject().ToDictionary(area => area.Name, area => Strings(area.Value))
        );
    }

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString() ?? "")];
}
