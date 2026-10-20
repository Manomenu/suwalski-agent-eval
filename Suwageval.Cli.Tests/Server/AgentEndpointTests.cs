using Suwageval.Cli.Server;

namespace Suwageval.Cli.Tests.Server;

public sealed class AgentEndpointTests
{
    [Fact]
    public void The_option_wins_over_the_variable() =>
        Assert.Equal("http://option", AgentEndpoint.Override("http://option", "http://variable"));

    [Fact]
    public void The_variable_answers_when_there_is_no_option() =>
        Assert.Equal("http://variable", AgentEndpoint.Override(" ", "http://variable"));

    [Fact]
    public void Without_either_it_says_nothing_so_the_caller_falls_back() => Assert.Null(AgentEndpoint.Override(null, ""));
}
