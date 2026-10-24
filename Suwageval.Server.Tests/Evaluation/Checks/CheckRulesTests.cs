using Suwageval.Contracts.Checks;
using static Suwageval.Server.Tests.Evaluation.Checks.Items;

namespace Suwageval.Server.Tests.Evaluation.Checks;

/// <summary>
/// The shared rules (<c>Suwageval.Contracts/Checks/CheckRules.cs</c>), tested here because Contracts has no test project of its
/// own. The CLI's <c>ValidatorTests</c> and <see cref="CheckValidationTests"/> show that each side reports their problems.
/// </summary>
public sealed class CheckRulesTests
{
    private static string[] Paths(CheckInput check) => [.. CheckRules.Problems(check, "checks[0]").Select(problem => problem.Path)];

    private static ToolCalledCheck Tool(string? argsJson = null, string? resultJson = null) =>
        new()
        {
            Key = "a",
            ScenarioKey = "s",
            Tool = "t",
            Args = argsJson is null ? null : Json(argsJson),
            Result = resultJson is null ? null : Json(resultJson),
        };

    [Fact]
    public void Valid_patterns_and_judges_in_args_data_and_result_are_fine()
    {
        Assert.Empty(
            Paths(
                Tool(
                    /*lang=json,strict*/
                    """{"zip":{"$pattern":"^[0-9]+$"},"why":{"$judge":{"description":"d"}}}""",
                    /*lang=json,strict*/
                    """{"x":{"$pattern":"ok"}}"""
                )
            )
        );
        Assert.Empty(
            Paths(
                new EventNotEmittedCheck
                {
                    Key = "b",
                    ScenarioKey = "s",
                    Event = "e",
                    Data = Json( /*lang=json,strict*/
                        """{"n":{"$pattern":"x"}}"""
                    ),
                }
            )
        );
    }

    [Fact]
    public void An_invalid_pattern_is_a_problem_at_its_path_in_args_data_and_result()
    {
        Assert.Equal(
            ["checks[0].args.zip.$pattern", "checks[0].result.items[0].$pattern"],
            Paths(
                Tool(
                    /*lang=json,strict*/
                    """{"zip":{"$pattern":"("}}""",
                    /*lang=json,strict*/
                    """{"items":[{"$pattern":"[a"}]}"""
                )
            )
        );
        Assert.Equal(
            ["checks[0].data.n.$pattern"],
            Paths(
                new EventCheck
                {
                    Key = "b",
                    ScenarioKey = "s",
                    Event = "e",
                    Data = Json( /*lang=json,strict*/
                        """{"n":{"$pattern":"*"}}"""
                    ),
                }
            )
        );
        Assert.Equal(
            ["checks[0].data.$pattern"],
            Paths(
                new EventNotEmittedCheck
                {
                    Key = "c",
                    ScenarioKey = "s",
                    Event = "e",
                    Data = Json( /*lang=json,strict*/
                        """{"$pattern":"("}"""
                    ),
                }
            )
        );
    }

    [Fact]
    public void A_malformed_judge_matcher_is_a_problem()
    {
        Assert.Equal(
            ["checks[0].args.a", "checks[0].args.b.$judge", "checks[0].args.c.$judge.description", "checks[0].args.d.$judge.examples"],
            Paths(
                Tool(
                    /*lang=json,strict*/
                    """{"a":{"$judge":{"description":"d"},"$pattern":"x"},"b":{"$judge":{}},"c":{"$judge":{"description":" "}},"d":{"$judge":{"examples":[]}}}"""
                )
            )
        );
    }

    [Fact]
    public void An_event_not_emitted_check_does_not_take_a_judge_matcher()
    {
        var problem = Assert.Single(
            CheckRules.Problems(
                new EventNotEmittedCheck
                {
                    Key = "c",
                    ScenarioKey = "s",
                    Event = "e",
                    Data = Json( /*lang=json,strict*/
                        """{"n":{"$judge":{"description":"d"}}}"""
                    ),
                },
                "checks[0]"
            )
        );

        Assert.Equal("checks[0].data", problem.Path);
    }

    [Fact]
    public void A_cited_check_needs_at_least_one_source_and_none_empty()
    {
        var none = new CitedCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Sources = [],
        };
        var blank = none with { Sources = ["plans", " "] };

        Assert.Equal(["checks[0].sources"], Paths(none));
        Assert.Equal(["checks[0].sources[1]"], Paths(blank));
        Assert.Empty(Paths(none with { Sources = ["plans"] }));
    }

    [Fact]
    public void A_text_check_needs_something_to_look_for_and_a_pattern_that_compiles()
    {
        var empty = new TextCheck { Key = "a", ScenarioKey = "s" };

        Assert.Equal(["checks[0]"], Paths(empty));
        Assert.Equal(["checks[0].pattern"], Paths(empty with { Pattern = "(" }));
        Assert.Empty(Paths(empty with { Contains = "x" }));
    }

    [Fact]
    public void A_judge_check_needs_criteria() =>
        Assert.Equal(
            ["checks[0].criteria"],
            Paths(
                new JudgeCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Criteria = " ",
                }
            )
        );

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void The_pass_threshold_is_above_zero_and_at_most_one(double threshold) =>
        Assert.Equal(
            ["checks[0].passThreshold"],
            Paths(
                new ToolNotCalledCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Tool = "t",
                    PassThreshold = threshold,
                }
            )
        );

    [Fact]
    public void The_key_is_kebab_case() =>
        Assert.Equal(
            ["checks[0].key"],
            Paths(
                new ToolNotCalledCheck
                {
                    Key = "Not Kebab",
                    ScenarioKey = "s",
                    Tool = "t",
                }
            )
        );

    [Fact]
    public void A_check_says_which_tool_event_and_anchors_it_names()
    {
        var where = new CheckWhere { After = "get_order", Before = "state.changed" };
        var called = Tool() with { By = "support-agent", Where = where };

        Assert.Equal(new ToolReference("t", "support-agent"), CheckRules.NamedTool(called));
        Assert.Equal(
            new ToolReference("t", null),
            CheckRules.NamedTool(
                new ToolNotCalledCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Tool = "t",
                }
            )
        );
        Assert.Equal(
            "e",
            CheckRules.NamedEvent(
                new EventCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Event = "e",
                }
            )
        );
        Assert.Null(CheckRules.NamedEvent(called));
        Assert.Null(
            CheckRules.NamedTool(
                new JudgeCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Criteria = "c",
                }
            )
        );
        Assert.Equal([("where.after", "get_order"), ("where.before", "state.changed")], CheckRules.Anchors(called));
        Assert.Empty(CheckRules.Anchors(Tool()));
    }
}
