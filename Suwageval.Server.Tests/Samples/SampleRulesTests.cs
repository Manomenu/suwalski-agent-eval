using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Samples;

namespace Suwageval.Server.Tests.Samples;

/// <summary>
/// The shared rules and the resolution order of <c>samples</c> (<c>Suwageval.Contracts/Samples</c>), tested here because Contracts has no
/// test project of its own.
/// </summary>
public sealed class SampleRulesTests
{
    [Theory]
    [InlineData(null, null, null, 3)]
    [InlineData(null, null, 5, 5)]
    [InlineData(null, 2, 5, 2)]
    [InlineData(1, 2, 5, 1)]
    [InlineData(4, null, null, 4)]
    public void The_most_specific_level_wins_and_three_is_the_default(int? check, int? scenario, int? agent, int expected) =>
        Assert.Equal(expected, SampleSettings.Effective(check, scenario, agent));

    [Fact]
    public void The_limits_are_one_to_twenty_with_a_default_of_three() =>
        Assert.Equal((1, 20, 3), (SampleSettings.Min, SampleSettings.Max, SampleSettings.Default));

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(20)]
    public void Not_set_and_the_limits_are_fine(int? samples) => Assert.Null(SampleRules.Problem(samples, "samples"));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Outside_the_limits_is_a_problem_that_names_the_path(int samples) =>
        Assert.Equal("checks[0].samples is from 1 to 20 (the default is 3).", SampleRules.Problem(samples, "checks[0].samples"));

    [Fact]
    public void A_check_is_checked_at_its_own_path() =>
        Assert.Equal(
            ["checks[2].samples"],
            CheckRules
                .Problems(
                    new TextCheck
                    {
                        Key = "c",
                        ScenarioKey = "s",
                        Contains = "x",
                        Samples = 0,
                    },
                    "checks[2]"
                )
                .Select(problem => problem.Path)
        );

    [Fact]
    public void An_agent_is_checked_at_samples() =>
        Assert.Equal(
            ["samples"],
            AgentRules
                .Problems(
                    new AgentDefinition
                    {
                        Name = "a",
                        EndpointUrl = "http://a.test",
                        Samples = 21,
                    }
                )
                .Select(problem => problem.Path)
        );
}
