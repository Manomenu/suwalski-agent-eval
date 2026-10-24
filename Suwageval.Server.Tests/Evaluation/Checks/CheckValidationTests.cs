using Suwageval.Contracts.Checks;
using Suwageval.Server.Evaluation.Checks;

namespace Suwageval.Server.Tests.Evaluation.Checks;

/// <summary>What the request adds to <see cref="CheckRules"/> (tested in <see cref="CheckRulesTests"/>): paths in the request, keys, scenario keys.</summary>
public sealed class CheckValidationTests
{
    private static string[] Fields(params CheckInput[] checks) => [.. CheckValidation.Validate(checks).Select(error => error.Field)];

    [Fact]
    public void The_rules_of_every_check_apply_at_their_index_in_the_request()
    {
        var good = new CitedCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Sources = ["plans"],
        };
        var none = good with { Key = "b", Sources = [] };

        Assert.Equal(["checks[1].sources"], Fields(good, none));
    }

    [Fact]
    public void A_key_used_twice_and_a_scenario_key_that_is_not_kebab_case_are_errors()
    {
        var first = new ToolNotCalledCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Tool = "t",
        };
        var second = first with { ScenarioKey = "Not Kebab" };

        Assert.Equal(["checks[1].key", "checks[1].scenarioKey"], Fields(first, second));
    }
}
