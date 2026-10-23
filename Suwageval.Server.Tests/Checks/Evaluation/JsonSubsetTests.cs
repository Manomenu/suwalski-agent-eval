using Suwageval.Server.Checks.Evaluation;

namespace Suwageval.Server.Tests.Checks.Evaluation;

public sealed class JsonSubsetTests
{
    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"a":1,"b":"x"}""", /*lang=json,strict*/
        """{"a":1}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""",
        """{}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":{"b":{"c":true},"d":2}}""", /*lang=json,strict*/
        """{"a":{"b":{"c":true}}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":[1,2,3]}""", /*lang=json,strict*/
        """{"a":[1,2,3]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":[{"id":1,"x":0},{"id":2,"x":0}]}""", /*lang=json,strict*/
        """{"a":[{"id":1},{"id":2}]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""", /*lang=json,strict*/
        """{"a":1.0}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":null}""", /*lang=json,strict*/
        """{"a":null}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":false}""", /*lang=json,strict*/
        """{"a":false}"""
    )]
    [InlineData("\"text\"", "\"text\"")]
    public void Contains_when_the_expected_is_a_subset(string actual, string expected) =>
        Assert.True(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""", /*lang=json,strict*/
        """{"b":1}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""", /*lang=json,strict*/
        """{"a":2}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":{"b":1}}""", /*lang=json,strict*/
        """{"a":{"b":2}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":{"b":1}}""", /*lang=json,strict*/
        """{"a":{"c":1}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":[1,2]}""", /*lang=json,strict*/
        """{"a":[1]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":[1,2]}""", /*lang=json,strict*/
        """{"a":[2,1]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":[{"id":1}]}""", /*lang=json,strict*/
        """{"a":[{"id":2}]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":"1"}""", /*lang=json,strict*/
        """{"a":1}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""", /*lang=json,strict*/
        """{"a":"1"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":null}""", /*lang=json,strict*/
        """{"a":0}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":true}""", /*lang=json,strict*/
        """{"a":false}"""
    )]
    [InlineData(
        """[1]""", /*lang=json,strict*/
        """{"a":1}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""",
        """[1]"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":"A-17"}""", /*lang=json,strict*/
        """{"a":"a-17"}"""
    )]
    public void Does_not_contain_when_anything_differs(string actual, string expected) =>
        Assert.False(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"reason":"cracked"}""", /*lang=json,strict*/
        """{"reason":{"$judge":{"description":"damaged"}}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"reason":null}""", /*lang=json,strict*/
        """{"reason":{"$judge":{"description":"damaged"}}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":{"b":[1,{"c":"x"}]}}""", /*lang=json,strict*/
        """{"a":{"b":[1,{"c":{"$judge":{"examples":["y"]}}}]}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"id":7,"extra":1}""", /*lang=json,strict*/
        """{"id":7,"extra":{"$judge":{"description":"anything"}}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":1}""", /*lang=json,strict*/
        """{"$judge":{"description":"the whole value"}}"""
    )]
    public void A_judge_matcher_only_needs_a_value_to_be_there(string actual, string expected) =>
        Assert.True(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"other":"cracked"}""", /*lang=json,strict*/
        """{"reason":{"$judge":{"description":"damaged"}}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"id":8,"reason":"x"}""", /*lang=json,strict*/
        """{"id":7,"reason":{"$judge":{"description":"damaged"}}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":[1]}""", /*lang=json,strict*/
        """{"a":[1,{"$judge":{"description":"x"}}]}"""
    )]
    public void Literals_next_to_a_judge_matcher_and_the_path_to_it_still_have_to_match(string actual, string expected) =>
        Assert.False(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Fact]
    public void An_object_with_judge_next_to_other_properties_is_a_literal_object_not_a_matcher()
    {
        var expected = Items.Json( /*lang=json,strict*/
            """{"reason":{"$judge":{"description":"x"},"other":1}}"""
        );

        Assert.False(
            JsonSubset.Contains(
                Items.Json( /*lang=json,strict*/
                    """{"reason":"cracked"}"""
                ),
                expected
            )
        );
        Assert.True(
            JsonSubset.Contains(
                Items.Json( /*lang=json,strict*/
                    """{"reason":{"$judge":{"description":"x"},"other":1,"more":2}}"""
                ),
                expected
            )
        );
        Assert.False(
            JsonSubset.Contains(
                Items.Json( /*lang=json,strict*/
                    """{"reason":{"$judge":{"description":"y"},"other":1}}"""
                ),
                expected
            )
        );
    }

    [Fact]
    public void Judgements_lists_every_matcher_with_its_path_the_value_found_there_and_what_is_asked()
    {
        var actual = Items.Json( /*lang=json,strict*/
            """{"id":7,"reason":"cracked","items":[{"note":"a"},{"note":"b"}]}"""
        );
        var expected = Items.Json( /*lang=json,strict*/
            """
            {"id":7,"reason":{"$judge":{"description":"damaged","examples":["broken",{"k":1}]}},
             "items":[{"note":{"$judge":{"examples":["a"]}}},{"note":"b"}]}
            """
        );

        var found = JsonSubset.Judgements(actual, expected, "args");

        Assert.Equal(["args.reason", "args.items[0].note"], found.Select(judgement => judgement.Path));
        Assert.Equal("cracked", found[0].Actual.GetString());
        Assert.Equal("damaged", found[0].Spec.Description);
        Assert.Equal(2, found[0].Spec.Examples.Count);
        Assert.Null(found[1].Spec.Description);
        Assert.Equal("a", found[1].Actual.GetString());
    }

    [Fact]
    public void A_matcher_at_the_root_is_the_path_of_the_root()
    {
        var found = JsonSubset.Judgements(
            Items.Json( /*lang=json,strict*/
                """{"a":1}"""
            ),
            Items.Json( /*lang=json,strict*/
                """{"$judge":{"description":"x"}}"""
            ),
            "data"
        );

        Assert.Equal("data", Assert.Single(found).Path);
    }

    [Fact]
    public void Numbers_too_large_for_a_decimal_are_compared_as_doubles()
    {
        Assert.True(JsonSubset.Contains(Items.Json("1e300"), Items.Json("1e300")));
        Assert.False(JsonSubset.Contains(Items.Json("1e300"), Items.Json("2e300")));
    }

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"zip":"12345"}""", /*lang=json,strict*/
        """{"zip":{"$pattern":"^12\\d{3}$"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"a":["x1","y"]}""", /*lang=json,strict*/
        """{"a":[{"$pattern":"^x\\d$"},"y"]}"""
    )]
    [InlineData(
        "\"Hello World\"", /*lang=json,strict*/
        """{"$pattern":"(?i)world"}"""
    )]
    public void A_pattern_matcher_matches_a_string_by_regular_expression(string actual, string expected) =>
        Assert.True(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"zip":"12346"}""", /*lang=json,strict*/
        """{"zip":{"$pattern":"^12345$"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"zip":12345}""", /*lang=json,strict*/
        """{"zip":{"$pattern":"12345"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"zip":{"x":"12345"}}""", /*lang=json,strict*/
        """{"zip":{"$pattern":"12345"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"zip":"12345"}""", /*lang=json,strict*/
        """{"zip":{"$pattern":"("}}"""
    )]
    public void A_pattern_matcher_does_not_match_what_the_expression_does_not_or_what_is_no_string(string actual, string expected) =>
        Assert.False(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Fact]
    public void A_pattern_that_takes_too_long_matches_nothing()
    {
        var actual = Items.Json($"\"{new string('a', 40)}!\"");

        Assert.False(
            JsonSubset.Contains(
                actual,
                Items.Json( /*lang=json,strict*/
                    """{"$pattern":"^(a+)+$"}"""
                )
            )
        );
    }

    [Fact]
    public void An_object_with_other_properties_next_to_pattern_is_an_ordinary_object()
    {
        var expected = Items.Json( /*lang=json,strict*/
            """{"$pattern":"x","other":1}"""
        );

        Assert.True(
            JsonSubset.Contains(
                Items.Json( /*lang=json,strict*/
                    """{"$pattern":"x","other":1}"""
                ),
                expected
            )
        );
        Assert.False(
            JsonSubset.Contains(
                Items.Json( /*lang=json,strict*/
                    """{"$pattern":"x"}"""
                ),
                expected
            )
        );
    }

    [Fact]
    public void Both_matchers_work_side_by_side_and_the_judgements_skip_the_pattern_nodes()
    {
        var actual = Items.Json( /*lang=json,strict*/
            """{"zip":" 12345 ","reason":"cracked"}"""
        );
        var expected = Items.Json( /*lang=json,strict*/
            """{"zip":{"$pattern":"^\\s*12345\\s*$"},"reason":{"$judge":{"description":"damaged"}}}"""
        );

        Assert.True(JsonSubset.Contains(actual, expected));
        Assert.Equal("result.reason", Assert.Single(JsonSubset.Judgements(actual, expected, "result")).Path);
    }

    [Fact]
    public void An_object_with_both_matchers_is_neither_and_the_judge_rule_reports_it()
    {
        var expected = Items.Json( /*lang=json,strict*/
            """{"reason":{"$judge":{"description":"d"},"$pattern":"x"}}"""
        );

        Assert.Null(JsonSubset.PatternOf(expected.GetProperty("reason")));
        Assert.False(JudgeMatcher.IsMatcher(expected.GetProperty("reason")));
    }
}
