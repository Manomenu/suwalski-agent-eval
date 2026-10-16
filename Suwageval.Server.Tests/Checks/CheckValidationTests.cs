using Suwageval.Contracts.Checks;
using Suwageval.Server.Checks;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Checks;

public sealed class CheckValidationTests
{
    private static string[] Fields(params CheckInput[] checks) => [.. CheckValidation.Validate(checks).Select(error => error.Field)];

    [Fact]
    public void Valid_patterns_in_args_data_and_result_are_fine()
    {
        Assert.Empty(
            Fields(
                new ToolCalledCheck
                {
                    Key = "a",
                    ScenarioKey = "s",
                    Tool = "t",
                    Args = Json( /*lang=json,strict*/
                        """{"zip":{"$pattern":"^[0-9]+$"}}"""
                    ),
                    Result = Json( /*lang=json,strict*/
                        """{"x":{"$pattern":"ok"}}"""
                    ),
                },
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
    public void An_invalid_pattern_is_an_error_at_its_path_in_args_data_and_result()
    {
        var fields = Fields(
            new ToolCalledCheck
            {
                Key = "a",
                ScenarioKey = "s",
                Tool = "t",
                Args = Json( /*lang=json,strict*/
                    """{"zip":{"$pattern":"("}}"""
                ),
                Result = Json( /*lang=json,strict*/
                    """{"items":[{"$pattern":"[a"}]}"""
                ),
            },
            new EventCheck
            {
                Key = "b",
                ScenarioKey = "s",
                Event = "e",
                Data = Json( /*lang=json,strict*/
                    """{"n":{"$pattern":"*"}}"""
                ),
            },
            new EventNotEmittedCheck
            {
                Key = "c",
                ScenarioKey = "s",
                Event = "e",
                Data = Json( /*lang=json,strict*/
                    """{"$pattern":"("}"""
                ),
            }
        );

        Assert.Equal(
            ["checks[0].args.zip.$pattern", "checks[0].result.items[0].$pattern", "checks[1].data.n.$pattern", "checks[2].data.$pattern"],
            fields
        );
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
        var blank = new CitedCheck
        {
            Key = "b",
            ScenarioKey = "s",
            Sources = ["plans", " "],
        };
        var good = new CitedCheck
        {
            Key = "c",
            ScenarioKey = "s",
            Sources = ["plans"],
        };

        Assert.Equal(["checks[0].sources", "checks[1].sources[1]"], Fields(none, blank, good));
    }
}
