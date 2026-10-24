using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Contracts;
using Suwageval.Server.Api;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests.Api;

public sealed class ServerAppTests(TestDatabase database) : IAsyncDisposable
{
    // Without the run worker: it would take the queued runs of the other tests (they share the database).
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private HttpClient Client()
    {
        // Skips (or fails in CI) without a database: the app does not start without one.
        _ = database.Pool;
        return app.CreateClient();
    }

    [Fact]
    public async Task Every_answer_names_the_servers_build()
    {
        var response = await Client().GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues(ServerVersion.Header, out var values));
        Assert.Equal(ServerVersion.Of(typeof(ServerApp).Assembly), Assert.Single(values));
    }

    [Fact]
    public async Task Health_answers_ok()
    {
        var response = await Client().GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Health("ok"), await response.Content.ReadFromJsonAsync<Health>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Routes_carry_no_api_prefix()
    {
        // /api is stripped at the edge (vite proxy, nginx). A route declared with it would be
        // reachable in no environment at all.
        var response = await Client().GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_evaluation_answers_under_eval_only()
    {
        var client = Client();
        const string path = "/runs/compare";

        var current = await client.GetAsync(new Uri("/eval" + path, UriKind.Relative), TestContext.Current.CancellationToken);
        var old = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        // The current address refuses a request naming no run (the route is there); the address before /eval is gone.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, current.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
