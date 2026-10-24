using System.Net;
using System.Text;
using Suwageval.Cli.Server;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Server;

public sealed class ServerClientTests
{
    private sealed class Answer(string body, string? serverVersion) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            if (serverVersion is not null)
            {
                response.Headers.Add(ServerVersion.Header, serverVersion);
            }
            return Task.FromResult(response);
        }
    }

    private static async Task<RunCreated> Create(string body, string? serverVersion)
    {
        using var handler = new Answer(body, serverVersion);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        return await new ServerClient(http).CreateAsync(new RunRequest { Agent = "shop" }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_newer_servers_extra_field_does_not_break_the_cli()
    {
        var created = await Create(
            /*lang=json,strict*/
            """{"runId":"0b7d4a52-8a6e-4f7e-9a51-0d3c6a0e2f10","agentVersionId":"5f1c2d3e-4b5a-6978-8a9b-0c1d2e3f4a5b","versionCreated":false,"queuePosition":3}""",
            "9.9.9+abc"
        );

        Assert.False(created.VersionCreated);
    }

    [Fact]
    public async Task An_answer_it_cannot_read_names_both_builds_and_the_update()
    {
        var failure = await Assert.ThrowsAsync<ServerClientException>(() =>
            Create( /*lang=json,strict*/
                """{"runId":"not a guid"}""",
                "9.9.9+abc"
            )
        );

        Assert.Contains("9.9.9+abc", failure.Message, StringComparison.Ordinal);
        Assert.Contains(ServerAnswers.CliVersion, failure.Message, StringComparison.Ordinal);
        Assert.Contains("just cli-install", failure.Message, StringComparison.Ordinal);
    }
}
