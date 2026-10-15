using AgentEval.Server.Db;
using AgentEval.Server.Settings;

namespace AgentEval.Server.Api;

/// <summary>Puts the server together: settings, the database, the routes.</summary>
/// <remarks>
/// Routes are declared without an /api prefix. The prefix belongs to the edge — the vite proxy
/// in development, nginx in the image — which strips it before the request lands here.
/// </remarks>
internal static class ServerApp
{
    public const string CorsPolicy = "web";

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // The layered .env files sit under every other source, so a real environment variable wins.
        builder.Configuration.Sources.Insert(0, DotEnv.Source(builder.Environment.ContentRootPath));

        var settings = ServerSettings.From(builder.Configuration);
        builder.WebHost.UseUrls($"http://{settings.Host}:{settings.Port}");

        builder.Services.AddSingleton(settings);
        builder.Services.AddDatabase(settings.DatabaseUrl);
        builder.Services.AddOpenApi();
        builder.Services.AddCors(cors =>
            cors.AddPolicy(CorsPolicy, policy => policy.WithOrigins([.. settings.CorsOrigins]).AllowAnyMethod().AllowAnyHeader())
        );

        var app = builder.Build();
        app.UseCors(CorsPolicy);
        app.MapOpenApi();

        // Touches nothing outside the process, so it doubles as the cluster's liveness probe.
        app.MapGet("/health", () => new Health("ok"));

        // Features map their routes here: app.MapScenarios() — and get a layer in ContractsTests.cs.
        return app;
    }
}
