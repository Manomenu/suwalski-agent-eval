using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Cli.Tests.Run;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Rerun;

/// <summary>The command against a stand-in for the server: what it sends, what it prints, which exit code it gives.</summary>
public sealed class RerunCommandTests
{
    private static readonly Guid OriginalId = Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555");
    private static readonly Guid NewId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid VersionId = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private sealed class FakeServer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri?.AbsolutePath ?? "", body));
            return answer(request);
        }
    }

    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, ContractJson.Options), Encoding.UTF8, "application/json"),
        };

    private static RunSummary Summary(RunStatus status) =>
        new()
        {
            Id = NewId,
            Agent = "example-agent",
            AgentVersionId = VersionId,
            EndpointUrl = "http://localhost:7010",
            Status = status,
            SamplesTotal = 1,
            SamplesDone = 1,
            ChecksPassed = 0,
            ChecksFailed = 0,
            CreatedAt = DateTime.UtcNow,
        };

    private static HttpResponseMessage Answer(HttpRequestMessage request, RunStatus status) =>
        (request.Method.Method, request.RequestUri?.AbsolutePath) switch
        {
            ("POST", var path) when path == $"/runs/{OriginalId}/rerun" => Json(
                new RunCreated
                {
                    RunId = NewId,
                    AgentVersionId = VersionId,
                    VersionCreated = false,
                },
                HttpStatusCode.Accepted
            ),
            ("GET", var path) when path == $"/runs/{NewId}" => Json(Summary(status)),
            ("GET", var path) when path == $"/runs/{NewId}/results" => Json(new RunResults { Run = Summary(status), Scenarios = [] }),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

    private static (int Exit, string Output, string Error, FakeServer Server) Run(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var server = new FakeServer(answer);
        using var http = new HttpClient(server, disposeHandler: false) { BaseAddress = new Uri("http://server.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["rerun", .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), server);
    }

    [Fact]
    public void Without_wait_it_posts_the_rerun_and_prints_the_version_and_the_new_run_id()
    {
        var (exit, output, error, server) = Run(request => Answer(request, RunStatus.Queued), OriginalId.ToString());

        Assert.Equal(0, exit);
        Assert.Equal($"version {VersionId}: existing{Environment.NewLine}{NewId}{Environment.NewLine}", output);
        Assert.Empty(error);
        var (method, path, _) = Assert.Single(server.Requests);
        Assert.Equal(("POST", $"/runs/{OriginalId}/rerun"), (method, path));
    }

    [Fact]
    public void An_endpoint_is_passed_on()
    {
        var (_, _, _, server) = Run(request => Answer(request, RunStatus.Queued), OriginalId.ToString(), "--endpoint", "http://other:1");

        using var sent = JsonDocument.Parse(server.Requests[0].Body ?? "");
        Assert.Equal("http://other:1", sent.RootElement.GetProperty("endpointUrl").GetString());
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void Only_strict_fails_on_a_known_failure(bool strict, int expected)
    {
        var (exit, _, _, _) = Run(
            request =>
                request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == $"/runs/{NewId}/results"
                    ? Json(KnownFailureResults.Create(Summary(RunStatus.Done)))
                    : Answer(request, RunStatus.Done),
            strict ? [OriginalId.ToString(), "--wait", "--strict"] : [OriginalId.ToString(), "--wait"]
        );

        Assert.Equal(expected, exit);
    }

    [Fact]
    public void With_wait_it_prints_the_verdict_and_the_link_and_exits_0()
    {
        var (exit, output, _, _) = Run(request => Answer(request, RunStatus.Done), OriginalId.ToString(), "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("0 of 0 checks passed.", output, StringComparison.Ordinal);
        Assert.Contains($"/runs/{NewId}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_fails_exits_2()
    {
        var (exit, output, _, _) = Run(request => Answer(request, RunStatus.Failed), OriginalId.ToString(), "--wait", "--json");

        Assert.Equal(2, exit);
        Assert.StartsWith("{", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusal_of_the_server_is_printed_and_exits_2()
    {
        var (exit, _, error, _) = Run(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent( /*lang=json,strict*/
                    """{"detail":"There is no run x."}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            },
            OriginalId.ToString()
        );

        Assert.Equal(2, exit);
        Assert.Contains("There is no run x.", error, StringComparison.Ordinal);
    }
}
