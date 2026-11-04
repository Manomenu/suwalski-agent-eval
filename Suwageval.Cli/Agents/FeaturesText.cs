using System.Globalization;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Agents;

internal static class FeaturesText
{
    public static string Of(AgentFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);
        var monitoring = features.Monitoring is { } settings
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"on (close after {settings.CloseAfter ?? "30m"}, environments {(settings.Environments is { } environments ? string.Join(", ", environments) : "any")})"
            )
            : "off";
        return $"features: evaluation {(features.Evaluation ? "on" : "off")}  monitoring {monitoring}";
    }
}
