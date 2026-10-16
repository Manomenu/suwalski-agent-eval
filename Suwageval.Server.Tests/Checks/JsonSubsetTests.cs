using Suwageval.Server.Checks;

namespace Suwageval.Server.Tests.Checks;

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

    [Fact]
    public void Numbers_too_large_for_a_decimal_are_compared_as_doubles()
    {
        Assert.True(JsonSubset.Contains(Items.Json("1e300"), Items.Json("1e300")));
        Assert.False(JsonSubset.Contains(Items.Json("1e300"), Items.Json("2e300")));
    }
}
