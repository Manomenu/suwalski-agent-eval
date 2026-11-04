using Suwageval.Server.Tests.Evaluation.Checks;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Tests.Verdicts;

public sealed class JsonSubsetTests
{
    [Theory]
    [InlineData("""{"a":1,"b":"x"}""", """{"a":1}""")]
    [InlineData("""{"a":1}""", """{}""")]
    [InlineData("""{"a":{"b":{"c":true},"d":2}}""", """{"a":{"b":{"c":true}}}""")]
    [InlineData("""{"a":[1,2,3]}""", """{"a":[1,2,3]}""")]
    [InlineData("""{"a":[{"id":1,"x":0},{"id":2,"x":0}]}""", """{"a":[{"id":1},{"id":2}]}""")]
    [InlineData("""{"a":1}""", """{"a":1.0}""")]
    [InlineData("""{"a":null}""", """{"a":null}""")]
    [InlineData("""{"a":false}""", """{"a":false}""")]
    [InlineData("\"text\"", "\"text\"")]
    public void Contains_when_the_expected_is_a_subset(string actual, string expected) =>
        Assert.True(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData("""{"a":1}""", """{"b":1}""")]
    [InlineData("""{"a":1}""", """{"a":2}""")]
    [InlineData("""{"a":{"b":1}}""", """{"a":{"b":2}}""")]
    [InlineData("""{"a":{"b":1}}""", """{"a":{"c":1}}""")]
    [InlineData("""{"a":[1,2]}""", """{"a":[1]}""")]
    [InlineData("""{"a":[1,2]}""", """{"a":[2,1]}""")]
    [InlineData("""{"a":[{"id":1}]}""", """{"a":[{"id":2}]}""")]
    [InlineData("""{"a":"1"}""", """{"a":1}""")]
    [InlineData("""{"a":1}""", """{"a":"1"}""")]
    [InlineData("""{"a":null}""", """{"a":0}""")]
    [InlineData("""{"a":true}""", """{"a":false}""")]
    [InlineData("""[1]""", """{"a":1}""")]
    [InlineData("""{"a":1}""", """[1]""")]
    [InlineData("""{"a":"A-17"}""", """{"a":"a-17"}""")]
    public void Does_not_contain_when_anything_differs(string actual, string expected) =>
        Assert.False(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData("""{"reason":"cracked"}""", """{"reason":{"$judge":{"description":"damaged"}}}""")]
    [InlineData("""{"reason":null}""", """{"reason":{"$judge":{"description":"damaged"}}}""")]
    [InlineData("""{"a":{"b":[1,{"c":"x"}]}}""", """{"a":{"b":[1,{"c":{"$judge":{"examples":["y"]}}}]}}""")]
    [InlineData("""{"id":7,"extra":1}""", """{"id":7,"extra":{"$judge":{"description":"anything"}}}""")]
    [InlineData("""{"a":1}""", """{"$judge":{"description":"the whole value"}}""")]
    public void A_judge_matcher_only_needs_a_value_to_be_there(string actual, string expected) =>
        Assert.True(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData("""{"other":"cracked"}""", """{"reason":{"$judge":{"description":"damaged"}}}""")]
    [InlineData("""{"id":8,"reason":"x"}""", """{"id":7,"reason":{"$judge":{"description":"damaged"}}}""")]
    [InlineData("""{"a":[1]}""", """{"a":[1,{"$judge":{"description":"x"}}]}""")]
    public void Literals_next_to_a_judge_matcher_and_the_path_to_it_still_have_to_match(string actual, string expected) =>
        Assert.False(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Fact]
    public void An_object_with_judge_next_to_other_properties_is_a_literal_object_not_a_matcher()
    {
        var expected = Items.Json("""{"reason":{"$judge":{"description":"x"},"other":1}}""");

        Assert.False(JsonSubset.Contains(Items.Json("""{"reason":"cracked"}"""), expected));
        Assert.True(JsonSubset.Contains(Items.Json("""{"reason":{"$judge":{"description":"x"},"other":1,"more":2}}"""), expected));
        Assert.False(JsonSubset.Contains(Items.Json("""{"reason":{"$judge":{"description":"y"},"other":1}}"""), expected));
    }

    [Fact]
    public void Judgements_lists_every_matcher_with_its_path_the_value_found_there_and_what_is_asked()
    {
        var actual = Items.Json("""{"id":7,"reason":"cracked","items":[{"note":"a"},{"note":"b"}]}""");
        var expected = Items.Json(
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
        var found = JsonSubset.Judgements(Items.Json("""{"a":1}"""), Items.Json("""{"$judge":{"description":"x"}}"""), "data");

        Assert.Equal("data", Assert.Single(found).Path);
    }

    [Fact]
    public void Numbers_too_large_for_a_decimal_are_compared_as_doubles()
    {
        Assert.True(JsonSubset.Contains(Items.Json("1e300"), Items.Json("1e300")));
        Assert.False(JsonSubset.Contains(Items.Json("1e300"), Items.Json("2e300")));
    }

    [Theory]
    [InlineData("""{"zip":"12345"}""", """{"zip":{"$pattern":"^12\\d{3}$"}}""")]
    [InlineData("""{"a":["x1","y"]}""", """{"a":[{"$pattern":"^x\\d$"},"y"]}""")]
    [InlineData("\"Hello World\"", """{"$pattern":"(?i)world"}""")]
    public void A_pattern_matcher_matches_a_string_by_regular_expression(string actual, string expected) =>
        Assert.True(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Theory]
    [InlineData("""{"zip":"12346"}""", """{"zip":{"$pattern":"^12345$"}}""")]
    [InlineData("""{"zip":12345}""", """{"zip":{"$pattern":"12345"}}""")]
    [InlineData("""{"zip":{"x":"12345"}}""", """{"zip":{"$pattern":"12345"}}""")]
    [InlineData("""{"zip":"12345"}""", """{"zip":{"$pattern":"("}}""")]
    public void A_pattern_matcher_does_not_match_what_the_expression_does_not_or_what_is_no_string(string actual, string expected) =>
        Assert.False(JsonSubset.Contains(Items.Json(actual), Items.Json(expected)));

    [Fact]
    public void A_pattern_that_takes_too_long_matches_nothing()
    {
        var actual = Items.Json($"\"{new string('a', 40)}!\"");

        Assert.False(JsonSubset.Contains(actual, Items.Json("""{"$pattern":"^(a+)+$"}""")));
    }

    [Fact]
    public void An_object_with_other_properties_next_to_pattern_is_an_ordinary_object()
    {
        var expected = Items.Json("""{"$pattern":"x","other":1}""");

        Assert.True(JsonSubset.Contains(Items.Json("""{"$pattern":"x","other":1}"""), expected));
        Assert.False(JsonSubset.Contains(Items.Json("""{"$pattern":"x"}"""), expected));
    }

    [Fact]
    public void Both_matchers_work_side_by_side_and_the_judgements_skip_the_pattern_nodes()
    {
        var actual = Items.Json("""{"zip":" 12345 ","reason":"cracked"}""");
        var expected = Items.Json("""{"zip":{"$pattern":"^\\s*12345\\s*$"},"reason":{"$judge":{"description":"damaged"}}}""");

        Assert.True(JsonSubset.Contains(actual, expected));
        Assert.Equal("result.reason", Assert.Single(JsonSubset.Judgements(actual, expected, "result")).Path);
    }

    [Fact]
    public void An_object_with_both_matchers_is_neither_and_the_judge_rule_reports_it()
    {
        var expected = Items.Json("""{"reason":{"$judge":{"description":"d"},"$pattern":"x"}}""");

        Assert.Null(JsonSubset.PatternOf(expected.GetProperty("reason")));
        Assert.False(JudgeMatcher.IsMatcher(expected.GetProperty("reason")));
    }
}
