using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Contracts;
using Suwageval.Server.Api;

namespace Suwageval.Server.Tests.Api;

public sealed class ServerAppTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private readonly WebApplicationFactory<Program> app = server.App;

    private HttpClient Client()
    {
        _ = database.DataSource;
        return app.CreateClient();
    }

    [Fact]
    public async Task Every_answer_names_the_servers_build()
    {
        var response = await Client().GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues(ServerVersion.Header, out var values));
        Assert.Equal(ServerVersion.Of(typeof(ServerApp).Assembly), Assert.Single(values));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task The_probes_of_compose_and_the_chart_answer_ok(string probe)
    {
        var response = await Client().GetAsync(new Uri(probe, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Routes_carry_no_api_prefix()
    {
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

        Assert.Equal(HttpStatusCode.UnprocessableEntity, current.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }
}
