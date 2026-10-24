using Suwageval.AgentSdk.Invocation;

namespace Suwageval.Server.Tests.Evaluation.Invocation;

public sealed class SuwagevalBaggageTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("a,b")]
    [InlineData("two words")]
    [InlineData("a=b")]
    [InlineData("100%")]
    [InlineData("%41")]
    [InlineData("a;b")]
    [InlineData("zażółć gęślą jaźń")]
    [InlineData("emoji 😀 ok")]
    [InlineData("quote\"back\\slash")]
    [InlineData("")]
    public void A_value_survives_the_round_trip(string value)
    {
        var header = SuwagevalBaggage.Encode([new("suwageval.run.group", value), new("other", "x")]);

        var decoded = SuwagevalBaggage.Decode([header]);

        Assert.Equal(value, decoded["suwageval.run.group"]);
        Assert.Equal("x", decoded["other"]);
        Assert.All(header, character => Assert.InRange(character, '!', '~'));
    }

    [Fact]
    public void Encodes_what_the_w3c_grammar_does_not_allow_and_nothing_else() =>
        Assert.Equal("k=a%2Cb%20c=d%25%C5%BC-_.~:/", SuwagevalBaggage.Encode([new("k", "a,b c=d%ż-_.~:/")]));

    [Fact]
    public void Reads_several_headers_drops_properties_and_skips_what_is_no_member()
    {
        var decoded = SuwagevalBaggage.Decode(["a=1;prop=x, b=2", "novalue,=empty,c=3", "a=4"]);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["a"] = "4",
                ["b"] = "2",
                ["c"] = "3",
            },
            decoded
        );
    }
}
