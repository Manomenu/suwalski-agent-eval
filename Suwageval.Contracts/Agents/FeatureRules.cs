using System.Globalization;
using System.Text.RegularExpressions;

namespace Suwageval.Contracts.Agents;

public static partial class FeatureRules
{
    public static readonly TimeSpan DefaultCloseAfter = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan MinCloseAfter = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan MaxCloseAfter = TimeSpan.FromDays(1);

    public const int MaxEnvironmentLength = 40;

    public static IReadOnlyList<(string Path, string Message)> Problems(AgentFeatures? features)
    {
        var problems = new List<(string Path, string Message)>();
        if (features?.Monitoring is not { } monitoring)
        {
            return problems;
        }
        if (monitoring.CloseAfter is { } closeAfter)
        {
            if (Duration(closeAfter) is not { } duration)
            {
                problems.Add(
                    ("features.monitoring.closeAfter", "closeAfter is a number with s, m or h (30m, 2h); leave it out for 30 minutes.")
                );
            }
            else if (duration < MinCloseAfter || duration > MaxCloseAfter)
            {
                problems.Add(("features.monitoring.closeAfter", "closeAfter is from 1 minute to 24 hours."));
            }
        }
        if (monitoring.Environments is { } environments)
        {
            if (environments.Count == 0)
            {
                problems.Add(
                    ("features.monitoring.environments", "environments names at least one environment; leave it out to take any.")
                );
            }
            for (var i = 0; i < environments.Count; i++)
            {
                if (EnvironmentProblem(environments[i]) is { } problem)
                {
                    problems.Add(($"features.monitoring.environments[{i}]", problem));
                }
                else if (environments.Take(i).Contains(environments[i], StringComparer.Ordinal))
                {
                    problems.Add(($"features.monitoring.environments[{i}]", $"The environment '{environments[i]}' is named twice."));
                }
            }
        }
        return problems;
    }

    public static string? EnvironmentProblem(string? environment) =>
        environment is not null && environment.Length <= MaxEnvironmentLength && EnvironmentName().IsMatch(environment)
            ? null
            : $"An environment is lowercase letters and digits, words joined by hyphens (production, demo-eu), at most {MaxEnvironmentLength} characters.";

    public static TimeSpan CloseAfter(MonitoringSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.CloseAfter is { } text && Duration(text) is { } duration ? duration : DefaultCloseAfter;
    }

    public static bool Accepts(MonitoringSettings settings, string environment)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Environments is not { } environments || environments.Contains(environment, StringComparer.Ordinal);
    }

    public static TimeSpan? Duration(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var match = DurationText().Match(text);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }
        return match.Groups[2].Value switch
        {
            "s" => TimeSpan.FromSeconds(amount),
            "m" => TimeSpan.FromMinutes(amount),
            _ => TimeSpan.FromHours(amount),
        };
    }

    [GeneratedRegex("^([0-9]{1,5})(s|m|h)$")]
    private static partial Regex DurationText();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex EnvironmentName();
}
