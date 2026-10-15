using System.Net;
using System.Net.Http.Json;
using AgentEval.Server.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentEval.Server.Tests.Api;

public sealed class ServerAppTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        host.UseSetting("DATABASE_URL", TestDatabase.DatabaseUrl)
    );

    private HttpClient Client()
    {
        // Skips (or fails in CI) without a database: the app does not start without one.
        _ = database.Pool;
        return app.CreateClient();
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

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
