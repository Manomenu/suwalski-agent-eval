using System.Net;
using System.Text.Json;

namespace Suwageval.Cli.Tests.Evaluation.Note;

/// <summary>The command against a stand-in for the API.</summary>
public sealed class NoteCommandTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class FakeApi(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add($"{request.Method} {request.RequestUri?.AbsolutePath} {body}");
            return new HttpResponseMessage(status);
        }
    }

    private static (int Exit, string Output, string Error, FakeApi Api) Note(HttpStatusCode status, params string[] args)
    {
        var api = new FakeApi(status);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["note", .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Fact]
    public void The_text_is_sent_to_the_run_and_its_page_printed()
    {
        var (exit, output, error, api) = Note(
            HttpStatusCode.NoContent,
            RunId.ToString(),
            "rebuilt the index",
            "--server",
            "https://eval.example/"
        );

        Assert.Equal(0, exit);
        Assert.Equal($"Set the note of run {RunId}.{Environment.NewLine}https://eval.example/runs/{RunId}{Environment.NewLine}", output);
        Assert.Empty(error);
        var request = Assert.Single(api.Requests);
        Assert.Equal($"PUT /runs/{RunId}/note", request[..request.IndexOf(' ', 4)]);
        Assert.Equal(
            "rebuilt the index",
            JsonDocument.Parse(request[request.IndexOf('{', StringComparison.Ordinal)..]).RootElement.GetProperty("note").GetString()
        );
    }

    [Fact]
    public void Clear_sends_no_note()
    {
        var (exit, output, _, api) = Note(HttpStatusCode.NoContent, RunId.ToString(), "--clear");

        Assert.Equal(0, exit);
        Assert.StartsWith($"Cleared the note of run {RunId}.", output, StringComparison.Ordinal);
        Assert.EndsWith( /*lang=json,strict*/
            "{\"note\":null}",
            Assert.Single(api.Requests),
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Text_and_clear_together_or_neither_are_refused_before_any_request(bool both)
    {
        var (exit, _, _, api) = Note(HttpStatusCode.NoContent, both ? [RunId.ToString(), "text", "--clear"] : [RunId.ToString()]);

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public void A_note_that_is_too_long_is_refused_before_any_request()
    {
        var (exit, _, error, api) = Note(HttpStatusCode.NoContent, RunId.ToString(), new string('x', 501));

        Assert.NotEqual(0, exit);
        Assert.Contains("500", error, StringComparison.Ordinal);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public void A_run_that_does_not_exist_exits_2()
    {
        var (exit, output, error, _) = Note(HttpStatusCode.NotFound, RunId.ToString(), "x");

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
    }

    private static string SampleDir => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    [Fact]
    public void A_group_note_goes_to_the_group_of_the_agent_in_agent_yaml_and_the_compare_page_is_printed()
    {
        var (exit, output, _, api) = Note(
            HttpStatusCode.NoContent,
            "--group",
            "sweep-1",
            "three models",
            "--dir",
            SampleDir,
            "--server",
            "https://eval.example/"
        );

        Assert.Equal(0, exit);
        Assert.Equal(
            $"Set the note of group sweep-1.{Environment.NewLine}https://eval.example/compare?group=sweep-1&agent=example-agent{Environment.NewLine}",
            output
        );
        var request = Assert.Single(api.Requests);
        Assert.StartsWith("PUT /agents/example-agent/run-groups/sweep-1/note ", request, StringComparison.Ordinal);
        Assert.EndsWith( /*lang=json,strict*/
            "{\"note\":\"three models\"}",
            request,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Clear_for_a_group_sends_no_note()
    {
        var (exit, output, _, api) = Note(HttpStatusCode.NoContent, "--group", "sweep-1", "--clear", "--dir", SampleDir);

        Assert.Equal(0, exit);
        Assert.StartsWith("Cleared the note of group sweep-1.", output, StringComparison.Ordinal);
        Assert.EndsWith( /*lang=json,strict*/
            "{\"note\":null}",
            Assert.Single(api.Requests),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_group_and_a_run_id_together_or_a_bad_group_name_are_refused_before_any_request()
    {
        var withRun = Note(HttpStatusCode.NoContent, "--group", "sweep-1", RunId.ToString(), "text", "--dir", SampleDir);
        var badName = Note(HttpStatusCode.NoContent, "--group", "Not Kebab", "text", "--dir", SampleDir);

        Assert.NotEqual(0, withRun.Exit);
        Assert.NotEqual(0, badName.Exit);
        Assert.Empty(withRun.Api.Requests);
        Assert.Empty(badName.Api.Requests);
    }

    [Fact]
    public void A_group_that_has_no_runs_exits_2()
    {
        var (exit, _, error, _) = Note(HttpStatusCode.NotFound, "--group", "sweep-1", "x", "--dir", SampleDir);

        Assert.Equal(2, exit);
        Assert.Contains("404", error, StringComparison.Ordinal);
    }
}
