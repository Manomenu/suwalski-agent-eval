using AgentEval.Server.Settings;
using Microsoft.Extensions.Configuration;

namespace AgentEval.Server.Tests.Settings;

public sealed class ServerSettingsTests
{
    private static ServerSettings From(params (string Key, string Value)[] values) =>
        ServerSettings.From(
            new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value))).Build()
        );

    [Fact]
    public void Every_setting_has_a_working_local_default() => Assert.Equal(ServerSettings.Defaults, From());

    [Fact]
    public void Configuration_overrides_the_defaults()
    {
        var settings = From(("PORT", "7000"), ("CORS_ORIGINS", "[\"http://a\", \"http://b\"]"), ("DATABASE_URL", "postgresql://u:p@db/x"));

        Assert.Equal((7000, "postgresql://u:p@db/x"), (settings.Port, settings.DatabaseUrl));
        Assert.Equal(["http://a", "http://b"], settings.CorsOrigins);
    }

    [Theory]
    [InlineData("PORT", "http")]
    [InlineData("PORT", "70000")]
    [InlineData("CORS_ORIGINS", "http://a")]
    public void A_malformed_value_stops_the_server(string key, string value) => Assert.Throws<FormatException>(() => From((key, value)));
}
