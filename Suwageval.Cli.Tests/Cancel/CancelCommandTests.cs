using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Cancel;

/// <summary>The command against a stand-in for the API.</summary>
public sealed class CancelCommandTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri?.AbsolutePath}");
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Summary(RunStatus status) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new RunSummary
                    {
                        Id = RunId,
                        Agent = "example-agent",
                        AgentVersionId = Guid.NewGuid(),
                        EndpointUrl = "http://localhost:7010",
                        Status = status,
                        SamplesTotal = 1,
                        SamplesDone = 0,
                        ChecksPassed = 0,
                        ChecksFailed = 0,
                        CreatedAt = DateTime.UtcNow,
                    },
                    ContractJson.Options
                ),
                Encoding.UTF8,
                "application/json"
            ),
        };

    private static Func<HttpRequestMessage, HttpResponseMessage> Api(RunStatus status) =>
        request =>
            request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == $"/runs/{RunId}/cancel"
                ? new HttpResponseMessage(HttpStatusCode.Accepted)
            : request.RequestUri?.AbsolutePath == $"/runs/{RunId}" ? Summary(status)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

    private static (int Exit, string Output, string Error, FakeApi Api) Cancel(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["cancel", .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Theory]
    [InlineData(RunStatus.Queued)]
    [InlineData(RunStatus.Running)]
    public void An_active_run_is_cancelled_and_its_page_printed(RunStatus status)
    {
        var (exit, output, error, api) = Cancel(Api(status), RunId.ToString(), "--server", "https://eval.example/");

        Assert.Equal(0, exit);
        Assert.Equal($"Cancelling run {RunId}.{Environment.NewLine}https://eval.example/runs/{RunId}{Environment.NewLine}", output);
        Assert.Empty(error);
        Assert.Contains($"POST /runs/{RunId}/cancel", api.Requests);
    }

    [Theory]
    [InlineData(RunStatus.Done)]
    [InlineData(RunStatus.Failed)]
    [InlineData(RunStatus.Cancelled)]
    public void A_finished_run_is_left_alone_and_said_so(RunStatus status)
    {
        var (exit, output, _, api) = Cancel(Api(status), RunId.ToString(), "--server", "https://eval.example/");

        Assert.Equal(0, exit);
        Assert.StartsWith($"Run {RunId} is already {status}.", output, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Requests, request => request.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public void A_run_that_does_not_exist_exits_2()
    {
        var (exit, output, error, _) = Cancel(_ => new HttpResponseMessage(HttpStatusCode.NotFound), RunId.ToString());

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_does_not_answer_exits_2()
    {
        var (exit, _, error, _) = Cancel(_ => throw new HttpRequestException("connection refused"), RunId.ToString());

        Assert.Equal(2, exit);
        Assert.Contains("could not be reached", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_id_that_is_not_one_is_refused_before_any_request()
    {
        var (exit, _, _, api) = Cancel(Api(RunStatus.Running), "not-a-run");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Requests);
    }
}
