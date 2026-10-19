using System.Globalization;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Trend;

/// <summary>One point of the trend as a line of <c>suwageval trend</c>.</summary>
internal static class TrendText
{
    public static string Line(RunPassRates point)
    {
        ArgumentNullException.ThrowIfNull(point);
        var tags = point.Tags.Count == 0 ? "" : "  tags: " + string.Join(", ", point.Tags.Select(tag => Rate(tag.Tag, tag.PassRate)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{point.RunId}  {point.CreatedAt:yyyy-MM-dd HH:mm}Z  version {point.VersionLabel ?? point.AgentVersionId.ToString()}  {point.PassRate * 100:0}% passed  median {TurnTime.Format(point.MedianMs)}  p95 {TurnTime.Format(point.P95Ms)}{tags}"
        );
    }

    private static string Rate(string tag, double rate) => string.Create(CultureInfo.InvariantCulture, $"{tag} {rate * 100:0}%");
}
