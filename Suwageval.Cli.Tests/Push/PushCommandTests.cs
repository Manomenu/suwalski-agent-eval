using System.Net;
using System.Text.Json;

namespace Suwageval.Cli.Tests.Push;

public sealed class PushCommandTests(PushServer server) : IDisposable
{
    private readonly string work = Directory.CreateTempSubdirectory("suwageval-push-").FullName;

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    public void Dispose() => Directory.Delete(work, recursive: true);

    /// <summary>The sample agent folder, copied, under a name of its own so tests do not meet in the shared database.</summary>
    private string Folder()
    {
        var dir = Path.Combine(work, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "scenarios"));
        File.WriteAllText(
            Path.Combine(dir, "agent.yaml"),
            File.ReadAllText(Path.Combine(Sample, "agent.yaml"))
                .Replace("name: example-agent", $"name: agent-{Guid.NewGuid():N}", StringComparison.Ordinal)
        );
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Sample, "scenarios")))
        {
            File.Copy(file, Path.Combine(dir, "scenarios", Path.GetFileName(file)));
        }
        return dir;
    }

    private static (int Exit, string Out, string Error) Run(HttpClient http, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(args, new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString());
    }

    private static int Count(string text, string word) => text.Split('\n').Count(line => line.EndsWith(word, StringComparison.Ordinal));

    [Fact]
    public void The_first_push_creates_everything_and_the_second_changes_nothing()
    {
        using var http = server.Client();
        var dir = Folder();

        var (firstExit, firstOut, firstError) = Run(http, "push", dir);
        var (secondExit, secondOut, _) = Run(http, "push", dir);

        Assert.Equal(0, firstExit);
        Assert.Empty(firstError);
        // The agent, the version, three scenarios, five checks.
        Assert.Equal(10, Count(firstOut, ": created"));
        Assert.Equal(0, secondExit);
        Assert.Equal(2, Count(secondOut, ": existing"));
        Assert.Equal(8, Count(secondOut, ": unchanged"));
    }

    [Fact]
    public void A_changed_scenario_is_updated_and_the_rest_stays_unchanged()
    {
        using var http = server.Client();
        var dir = Folder();
        Run(http, "push", dir);
        var file = Path.Combine(dir, "scenarios", "refund-happy-path.yaml");
        File.WriteAllText(file, File.ReadAllText(file).Replace("I'd like a refund.", "Please refund it.", StringComparison.Ordinal));

        var (exit, output, _) = Run(http, "push", dir);

        Assert.Equal(0, exit);
        Assert.Contains("scenario refund-happy-path: updated", output, StringComparison.Ordinal);
        Assert.Equal(1, Count(output, ": updated"));
        Assert.Equal(7, Count(output, ": unchanged"));
    }

    [Fact]
    public void Json_prints_one_object_with_the_outcomes()
    {
        using var http = server.Client();

        var (exit, output, _) = Run(http, "push", Folder(), "--json");

        Assert.Equal(0, exit);
        var result = JsonDocument.Parse(output).RootElement;
        Assert.True(result.GetProperty("agentCreated").GetBoolean());
        Assert.True(result.GetProperty("versionCreated").GetBoolean());
        Assert.Equal(3, result.GetProperty("scenarios").GetArrayLength());
        Assert.Equal(5, result.GetProperty("checks").GetArrayLength());
        Assert.All(
            result.GetProperty("checks").EnumerateArray(),
            check => Assert.Equal("created", check.GetProperty("outcome").GetString())
        );
    }

    [Fact]
    public void An_answer_the_server_refuses_is_exit_1_with_its_message()
    {
        using var http = server.Client();
        var dir = Folder();
        var agent = Path.Combine(dir, "agent.yaml");
        File.WriteAllText(agent, File.ReadAllText(agent).Replace("http://localhost:7010", "ftp://nowhere", StringComparison.Ordinal));

        var (exit, output, error) = Run(http, "push", dir);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Contains("422", error, StringComparison.Ordinal);
        Assert.Contains("endpointUrl", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_files_stop_the_push_before_the_server_is_asked()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler);
        var dir = Folder();
        File.WriteAllText(Path.Combine(dir, "scenarios", "refund-happy-path.yaml"), "key: Not Kebab\nconversation: []\n");

        var (exit, _, error) = Run(http, "push", dir);

        Assert.Equal(1, exit);
        Assert.Contains("kebab-case", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_cannot_be_reached_is_exit_1()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://nowhere.test/") };

        var (exit, _, error) = Run(http, "push", Folder());

        Assert.Equal(1, exit);
        Assert.Contains("Cannot reach the server at http://nowhere.test/", error, StringComparison.Ordinal);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused", null, HttpStatusCode.ServiceUnavailable);
    }
}
