using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class SamplePlanTests
{
    private static ScenarioContent Scenario(int? samples) => new() { Conversation = [new UserMessage { Text = "hi" }], Samples = samples };

    private static (CheckRow Row, CheckInput Input) Check(string key, int? samples) =>
        (
            new CheckRow(Guid.NewGuid(), Guid.NewGuid(), key, "s", 1, "{}", "hash", DateTime.UtcNow),
            new TextCheck
            {
                Key = key,
                ScenarioKey = "s",
                Contains = "x",
                Samples = samples,
            }
        );

    [Fact]
    public void A_check_without_samples_takes_its_scenarios_then_the_agents_then_three()
    {
        var check = Check("c", null).Input;

        Assert.Equal(2, SamplePlan.Effective(check, Scenario(2), 5));
        Assert.Equal(5, SamplePlan.Effective(check, Scenario(null), 5));
        Assert.Equal(3, SamplePlan.Effective(check, Scenario(null), null));
    }

    [Fact]
    public void A_checks_own_samples_win_over_the_scenarios_and_the_agents() =>
        Assert.Equal(1, SamplePlan.Effective(Check("c", 1).Input, Scenario(4), 5));

    [Fact]
    public void The_calls_are_the_most_any_check_is_judged_on()
    {
        IReadOnlyList<(CheckRow Row, CheckInput Input)> checks = [Check("a", 1), Check("b", 2), Check("c", null)];

        Assert.Equal(2, SamplePlan.Calls(Scenario(1), checks, null));
        Assert.Equal(4, SamplePlan.Calls(Scenario(4), checks, null));
        Assert.Equal(3, SamplePlan.Calls(Scenario(null), checks, null));
    }

    [Fact]
    public void A_scenario_without_checks_is_called_its_own_number_of_times()
    {
        Assert.Equal(2, SamplePlan.Calls(Scenario(2), [], 5));
        Assert.Equal(5, SamplePlan.Calls(Scenario(null), [], 5));
        Assert.Equal(3, SamplePlan.Calls(Scenario(null), [], null));
    }

    [Fact]
    public void A_sample_is_judged_by_the_checks_whose_number_reaches_it()
    {
        IReadOnlyList<(CheckRow Row, CheckInput Input)> checks = [Check("a", 1), Check("b", 2), Check("c", null)];

        Assert.Equal(["a", "b", "c"], SamplePlan.JudgedOn(1, Scenario(3), checks, null).Select(check => check.Input.Key));
        Assert.Equal(["b", "c"], SamplePlan.JudgedOn(2, Scenario(3), checks, null).Select(check => check.Input.Key));
        Assert.Equal(["c"], SamplePlan.JudgedOn(3, Scenario(3), checks, null).Select(check => check.Input.Key));
    }
}
