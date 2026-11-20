using System.Text.Json;

namespace Suwageval.Gate.Design;

internal static class LayerOrders
{
    public const string Path = "layers.json";

    private static readonly Lazy<JsonDocument> Document = new(() => JsonDocument.Parse(GuardTests.Repository.Read(Path)));

    public static LayerOrder Server => Order("Suwageval.Server");

    public static LayerOrder Cli => Order("Suwageval.Cli");

    private static LayerOrder Order(string project)
    {
        var order = Document.Value.RootElement.GetProperty("projects").GetProperty(project);
        return new LayerOrder(
            Strings(order.GetProperty("layers")),
            order.GetProperty("areas").EnumerateObject().ToDictionary(area => area.Name, area => Strings(area.Value))
        );
    }

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString() ?? "")];
}
