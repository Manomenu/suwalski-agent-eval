using System.Reflection;
using Suwageval.Contracts;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Database;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Api;

/// <summary>Puts the server together: configuration, the database, the routes.</summary>
/// <remarks>
/// Routes are declared without an /api prefix. The prefix belongs to the edge — the vite proxy
/// in development, nginx in the image — which strips it before the request lands here.
/// </remarks>
internal static class ServerApp
{
    /// <summary>
    /// The build writes the OpenAPI document (scripts/.internal/api-types.sh) by starting the app inside
    /// its GetDocument tool. Only the routes matter then, so the database is not touched.
    /// </summary>
    private static bool WritingOpenApiDocument => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddDatabase(builder.Configuration, migrateOnStart: !WritingOpenApiDocument);
        builder.Services.AddSingleton<AgentStore>().AddSingleton<ScenarioStore>().AddSingleton<CheckStore>();
        builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.UseStatusCodePages();
        app.MapOpenApi();

        // Touches nothing outside the process, so it doubles as the cluster's liveness probe.
        app.MapGet("/health", () => new Health("ok"));

        // Features map their routes here: app.MapScenarios() — and get a layer in ContractsTests.cs.
        app.MapAgents();
        app.MapScenarios();
        app.MapChecks();
        return app;
    }
}
