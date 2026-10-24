using System.Net;

namespace Suwageval.Cli.Tests.Evaluation.Push;

/// <summary>The push command without a server: what it does before it asks, and when it cannot reach one. The rest is the end-to-end tests'.</summary>
public sealed class PushCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static (int Exit, string Error) Run(HttpClient http, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(args, new StringReader(""), output, error, http);
        return (exit, error.ToString());
    }

    [Fact]
    public void Invalid_files_stop_the_push_before_the_server_is_asked()
    {
        using var work = new TempFolder();
        File.Copy(Path.Combine(Sample, "agent.yaml"), Path.Combine(work.Path, "agent.yaml"));
        Directory.CreateDirectory(Path.Combine(work.Path, "scenarios"));
        File.WriteAllText(Path.Combine(work.Path, "scenarios", "refund-happy-path.yaml"), "key: Not Kebab\nconversation: []\n");
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler);

        var (exit, error) = Run(http, "push", work.Path);

        Assert.Equal(1, exit);
        Assert.Contains("kebab-case", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_cannot_be_reached_is_exit_1()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://nowhere.test/") };

        var (exit, error) = Run(http, "push", Sample);

        Assert.Equal(1, exit);
        Assert.Contains("The server could not be reached", error, StringComparison.Ordinal);
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("suwageval-push-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused", null, HttpStatusCode.ServiceUnavailable);
    }
}
