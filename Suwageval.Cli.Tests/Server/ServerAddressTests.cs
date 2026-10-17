using Suwageval.Cli.Files;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Tests.Server;

public sealed class ServerAddressTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("suwageval-address-").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private static Func<string?> FromFile(string? url) => () => url;

    [Fact]
    public void The_option_wins_over_everything() =>
        Assert.Equal(new Uri("http://option/"), ServerAddress.Site("http://option", "http://variable", FromFile("http://file")));

    [Fact]
    public void The_variable_wins_over_the_file() =>
        Assert.Equal(new Uri("http://variable/"), ServerAddress.Site(null, "http://variable", FromFile("http://file")));

    [Fact]
    public void The_file_wins_over_the_default() =>
        Assert.Equal(new Uri("http://file/"), ServerAddress.Site(null, "", FromFile("http://file/")));

    [Fact]
    public void Without_anything_it_is_the_default() =>
        Assert.Equal(new Uri(ServerAddress.Default + "/"), ServerAddress.Site(null, null, FromFile(null)));

    [Fact]
    public void The_file_is_not_read_when_the_option_or_the_variable_answers()
    {
        ServerAddress.Site("http://option", null, () => throw new InvalidOperationException("read"));
        ServerAddress.Site(null, "http://variable", () => throw new InvalidOperationException("read"));
    }

    [Fact]
    public void The_suwageval_url_of_the_agent_yaml_in_a_folder_is_read()
    {
        File.WriteAllText(Path.Combine(dir, "agent.yaml"), "name: x\nendpointUrl: http://a:1\nsuwagevalUrl: http://suwageval.example\n");

        Assert.Equal("http://suwageval.example", AgentReader.SuwagevalUrl(dir));
    }

    [Fact]
    public void A_missing_file_or_a_value_that_is_not_an_address_says_nothing()
    {
        Assert.Null(AgentReader.SuwagevalUrl(dir));
        File.WriteAllText(Path.Combine(dir, "agent.yaml"), "name: x\nsuwagevalUrl: not-an-address\n");
        Assert.Null(AgentReader.SuwagevalUrl(dir));
    }
}
