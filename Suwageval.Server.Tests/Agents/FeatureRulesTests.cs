using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Tests.Agents;

/// <summary><see cref="FeatureRules"/>, shared by the CLI's <c>validate</c> and the server's push.</summary>
public sealed class FeatureRulesTests
{
    private static IReadOnlyList<string> Paths(MonitoringSettings monitoring) =>
        [.. FeatureRules.Problems(new AgentFeatures { Monitoring = monitoring }).Select(problem => problem.Path)];

    [Fact]
    public void No_features_and_empty_monitoring_settings_are_valid()
    {
        Assert.Empty(FeatureRules.Problems(null));
        Assert.Empty(FeatureRules.Problems(new AgentFeatures { Evaluation = false }));
        Assert.Empty(Paths(new MonitoringSettings()));
        Assert.Empty(Paths(new MonitoringSettings { CloseAfter = "90s", Environments = ["production", "demo-eu"] }));
    }

    [Theory]
    [InlineData("30")]
    [InlineData("30 m")]
    [InlineData("1d")]
    [InlineData("30s")]
    [InlineData("25h")]
    public void A_close_after_that_is_no_duration_or_out_of_range_is_a_problem(string closeAfter) =>
        Assert.Equal(["features.monitoring.closeAfter"], Paths(new MonitoringSettings { CloseAfter = closeAfter }));

    [Fact]
    public void Environments_are_named_at_least_once_each_in_lowercase_words()
    {
        Assert.Equal(["features.monitoring.environments"], Paths(new MonitoringSettings { Environments = [] }));
        Assert.Equal(
            ["features.monitoring.environments[0]", "features.monitoring.environments[1]", "features.monitoring.environments[3]"],
            Paths(new MonitoringSettings { Environments = ["Production", "demo_eu", "staging", "staging"] })
        );
    }

    [Fact]
    public void Close_after_defaults_to_30_minutes_and_environments_to_any()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), FeatureRules.CloseAfter(new MonitoringSettings()));
        Assert.Equal(TimeSpan.FromHours(2), FeatureRules.CloseAfter(new MonitoringSettings { CloseAfter = "2h" }));
        Assert.True(FeatureRules.Accepts(new MonitoringSettings(), "anything"));
        Assert.True(FeatureRules.Accepts(new MonitoringSettings { Environments = ["production"] }, "production"));
        Assert.False(FeatureRules.Accepts(new MonitoringSettings { Environments = ["production"] }, "staging"));
    }
}
