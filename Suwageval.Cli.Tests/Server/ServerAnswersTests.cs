using System.Text.Json;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Server;

public sealed class ServerAnswersTests
{
    [Fact]
    public void A_field_the_cli_does_not_know_is_skipped()
    {
        var answer = JsonSerializer.Deserialize<RunCreated>(
            """{"runId":"0b7d4a52-8a6e-4f7e-9a51-0d3c6a0e2f10","agentVersionId":"5f1c2d3e-4b5a-6978-8a9b-0c1d2e3f4a5b","versionCreated":true,"addedLater":42}""",
            ServerAnswers.Options
        );

        Assert.NotNull(answer);
        Assert.True(answer.VersionCreated);
    }

    [Fact]
    public void A_missing_required_field_is_still_an_error()
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<RunCreated>("""{"runId":"0b7d4a52-8a6e-4f7e-9a51-0d3c6a0e2f10"}""", ServerAnswers.Options)
        );
    }

    [Fact]
    public void An_unreadable_answer_from_another_build_says_how_to_update_the_cli()
    {
        var message = ServerAnswers.Unreadable("bad value", "9.9.9+abc");

        Assert.Contains("The server's answer could not be read: bad value", message, StringComparison.Ordinal);
        Assert.Contains("probably older than the server (9.9.9+abc)", message, StringComparison.Ordinal);
        Assert.Contains("just cli-install", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_does_not_name_its_build_gets_the_hint_too() =>
        Assert.Contains("(build unknown)", ServerAnswers.Unreadable("bad value", null), StringComparison.Ordinal);

    [Fact]
    public void An_unreadable_answer_from_the_same_build_is_not_blamed_on_the_cli()
    {
        var message = ServerAnswers.Unreadable("bad value", ServerAnswers.CliVersion);

        Assert.Equal("The server's answer could not be read: bad value", message);
    }
}
