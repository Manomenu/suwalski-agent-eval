using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.AgentDelete;

/// <summary><c>suwageval delete-agent</c> against a stand-in for the API.</summary>
public sealed class AgentDeleteCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private sealed record Call(HttpMethod Method, string Path);

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(new Call(request.Method, request.RequestUri?.AbsolutePath ?? ""));
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Answer(HttpRequestMessage request) =>
        request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new AgentDeletion(Guid.NewGuid(), "example-agent", 2, 5, 7, 3, 12, 36), ContractJson.Options),
                    Encoding.UTF8,
                    "application/json"
                ),
            };

    private static (int Exit, string Output, string Error, FakeApi Api) Delete(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        string typed,
        bool interactive,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(
            ["delete-agent", .. args, "--dir", Sample],
            new StringReader(typed),
            output,
            error,
            http,
            interactive: interactive
        );
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Fact]
    public void Dry_run_prints_what_would_go_and_deletes_nothing()
    {
        var (exit, output, error, api) = Delete(Answer, "", interactive: true, "--dry-run");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains(
            "would delete: 2 version(s), 5 scenario(s), 7 check(s), 3 run(s), 12 sample(s), 36 verdict(s)",
            output,
            StringComparison.Ordinal
        );
        var call = Assert.Single(api.Calls);
        Assert.Equal((HttpMethod.Get, "/agents/example-agent/deletion"), (call.Method, call.Path));
    }

    [Fact]
    public void Yes_deletes_without_asking()
    {
        var (exit, output, _, api) = Delete(Answer, "", interactive: false, "--yes");

        Assert.Equal(0, exit);
        Assert.Contains("deleted agent example-agent", output, StringComparison.Ordinal);
        Assert.Equal(
            [(HttpMethod.Get, "/agents/example-agent/deletion"), (HttpMethod.Delete, "/agents/example-agent")],
            api.Calls.Select(call => (call.Method, call.Path))
        );
    }

    [Fact]
    public void Without_a_terminal_and_without_yes_it_exits_1_and_deletes_nothing()
    {
        var (exit, _, error, api) = Delete(Answer, "example-agent\n", interactive: false);

        Assert.Equal(1, exit);
        Assert.Contains("--yes", error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Fact]
    public void In_a_terminal_it_deletes_when_the_name_is_typed()
    {
        var (exit, output, _, api) = Delete(Answer, "example-agent\n", interactive: true);

        Assert.Equal(0, exit);
        Assert.Contains("Type the agent's name to confirm", output, StringComparison.Ordinal);
        Assert.Contains(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData("yes\n")]
    [InlineData("Example-Agent\n")]
    [InlineData("")]
    public void In_a_terminal_anything_but_the_name_exits_1_and_deletes_nothing(string typed)
    {
        var (exit, _, error, api) = Delete(Answer, typed, interactive: true);

        Assert.Equal(1, exit);
        Assert.Contains("Not deleted", error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Fact]
    public void A_refusal_of_the_server_exits_2_with_its_message_and_deletes_nothing()
    {
        var (exit, _, error, api) = Delete(
            _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    /*lang=json,strict*/
                    """{"title":"Agent has active runs","detail":"A run of this agent is queued or running: cancel it or wait until it ends."}""",
                    Encoding.UTF8,
                    "application/problem+json"
                ),
            },
            "",
            interactive: true,
            "--yes"
        );

        Assert.Equal(2, exit);
        Assert.Contains("queued or running", error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }
}
