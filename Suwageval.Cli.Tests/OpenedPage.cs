using System.Net;
using System.Text;

namespace Suwageval.Cli.Tests;

/// <summary>Runs a command with <c>--open</c> against a stand-in API that answers every request with one JSON body, recording the pages the browser was asked to open.</summary>
internal static class OpenedPage
{
    public const string Site = "https://eval.example/";

    public static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private sealed class Answer(List<string> paths, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
            );
        }
    }

    /// <param name="json">The body every request is answered with.</param>
    /// <param name="args">The command and its arguments; <c>--open</c> and <c>--server</c> are added.</param>
    public static (int Exit, string Output, string Error, List<Uri> Opened, List<string> Paths) Run(string json, params string[] args)
    {
        var paths = new List<string>();
        var opened = new List<Uri>();
        using var handler = new Answer(paths, json);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run([.. args, "--open", "--server", Site], new StringReader(""), output, error, http, opened.Add);
        return (exit, output.ToString(), error.ToString(), opened, paths);
    }
}
