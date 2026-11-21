using System.Net;
using System.Text;
using Suwageval.Cli.Server;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Server;

public sealed class ServerClientTests
{
    private sealed class Answer(string body, string? serverVersion, HttpStatusCode status = HttpStatusCode.Accepted) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (serverVersion is not null)
            {
                response.Headers.Add(ServerVersion.Header, serverVersion);
            }
            return Task.FromResult(response);
        }
    }

    private static async Task<RunCreated> Create(string body, string? serverVersion, HttpStatusCode status = HttpStatusCode.Accepted)
    {
        using var handler = new Answer(body, serverVersion, status);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        return await new ServerClient(http).CreateAsync(new RunRequest { Agent = "shop" }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_newer_servers_extra_field_does_not_break_the_cli()
    {
        var created = await Create(
            """{"runId":"0b7d4a52-8a6e-4f7e-9a51-0d3c6a0e2f10","agentVersionId":"5f1c2d3e-4b5a-6978-8a9b-0c1d2e3f4a5b","versionCreated":false,"queuePosition":3}""",
            "9.9.9+abc"
        );

        Assert.False(created.VersionCreated);
    }

    [Fact]
    public async Task An_answer_it_cannot_read_names_both_builds_and_the_update()
    {
        var failure = await Assert.ThrowsAsync<ServerClientException>(() => Create("""{"runId":"not a guid"}""", "9.9.9+abc"));

        Assert.Contains("9.9.9+abc", failure.Message, StringComparison.Ordinal);
        Assert.Contains(ServerAnswers.CliVersion, failure.Message, StringComparison.Ordinal);
        Assert.Contains("just cli-install", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refusal_prints_where_each_mistake_is_what_is_wrong_and_its_fix()
    {
        var failure = await Assert.ThrowsAsync<ServerClientException>(() =>
            Create(
                """{"title":"The request is not valid","detail":"2 mistakes in `mistakes`: fix each as its `fix` says, then try again.","code":"request.invalid","mistakes":[{"code":"field.invalid","message":"The page is below 1.","fix":"Ask for page 1 or later.","at":"page"},{"code":"field.invalid","message":"No fix here."},{"code":"version.mismatch","message":"check 'a' names tool 'x'.","fix":"Make the check fit."}]}""",
                null,
                HttpStatusCode.UnprocessableEntity
            )
        );

        Assert.Equal(
            "The server answered 422 UnprocessableEntity: The request is not valid:\n  - page: The page is below 1. Ask for page 1 or later."
                + "\n  - check 'a' names tool 'x'. Make the check fit.",
            failure.Message
        );
        Assert.Null(failure.Feature);
    }

    [Fact]
    public async Task A_refusal_without_mistakes_prints_its_detail_and_names_the_feature_that_is_off()
    {
        var failure = await Assert.ThrowsAsync<ServerClientException>(() =>
            Create("""{"title":"Feature off","detail":"Monitoring is off.","code":"monitoring.off"}""", null, HttpStatusCode.Conflict)
        );

        Assert.Equal("The server answered 409 Conflict: Monitoring is off.", failure.Message);
        Assert.Equal("monitoring", failure.Feature);
    }

    [Theory]
    [InlineData(
        """{"title":"Newer","detail":"A code this CLI does not know.","code":"something.new"}""",
        ": A code this CLI does not know."
    )]
    [InlineData("<html>busy</html>", "")]
    public async Task An_unknown_code_or_an_answer_that_is_no_refusal_prints_what_it_can(string body, string printed)
    {
        var failure = await Assert.ThrowsAsync<ServerClientException>(() => Create(body, null, HttpStatusCode.ServiceUnavailable));

        Assert.Equal($"The server answered 503 ServiceUnavailable{printed}", failure.Message);
        Assert.Null(failure.Feature);
    }
}
