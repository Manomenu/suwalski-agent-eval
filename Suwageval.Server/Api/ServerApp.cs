using System.Reflection;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts;
using Suwageval.Server.Agents;
using Suwageval.Server.Agents.Versions;
using Suwageval.Server.Database;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Runs.AgentErrors;
using Suwageval.Server.Evaluation.Runs.Compare;
using Suwageval.Server.Evaluation.Runs.Groups;
using Suwageval.Server.Evaluation.Runs.Trend;
using Suwageval.Server.Evaluation.Runs.Unstable;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Monitoring.Conversations;

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
        builder
            .Services.AddSingleton<AgentStore>()
            .AddSingleton<ScenarioStore>()
            .AddSingleton<CheckStore>()
            .AddSingleton<RunGroupStore>()
            .AddSingleton<TrendStore>()
            .AddSingleton<UnstableStore>()
            .AddSingleton<AgentErrorStore>()
            .AddSingleton<VersionStore>();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            ContractJson.Configure(options.SerializerOptions);
            // An unset field is absent from a response, not null. (Not in ContractJson.Options: it also serializes what is hashed.)
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddRuns(builder.Configuration, startWorker: !WritingOpenApiDocument);
        builder.Services.AddConversations(startCloser: !WritingOpenApiDocument);
        builder.Services.AddOpenApi();

        var app = builder.Build();
        // Every answer says which build sent it: the CLI names it when it cannot read an answer (ServerVersion).
        var build = ServerVersion.Of(typeof(ServerApp).Assembly);
        app.Use(
            (context, next) =>
            {
                context.Response.Headers[ServerVersion.Header] = build;
                return next(context);
            }
        );
        app.UseStatusCodePages();
        app.MapOpenApi();

        // Touches nothing outside the process, so it doubles as the cluster's liveness probe.
        app.MapGet("/health", () => new Health("ok"));

        // Features map their routes here: app.MapAgents(), the Evaluation area's in MapEvaluation — and get a layer in ContractsTests.cs.
        app.MapAgents();
        app.MapVersions();
        MapEvaluation(app.MapGroup("/eval"));
        // The evaluation's addresses before /eval, for a CLI installed before it: out of the OpenAPI document, so nothing
        // new is built on them, and removed on epic/monitoring-tab with its other breaking changes.
        MapEvaluation(app.MapGroup("").ExcludeFromDescription());
        app.MapGroup("/monitoring").MapConversations();
        return app;
    }

    /// <summary>The routes of the Evaluation area, under the prefix the group gives them.</summary>
    private static void MapEvaluation(IEndpointRouteBuilder evaluation)
    {
        evaluation.MapScenarios();
        evaluation.MapChecks();
        evaluation.MapRuns();
        evaluation.MapCompare();
        evaluation.MapRunGroups();
        evaluation.MapTrend();
        evaluation.MapUnstable();
        evaluation.MapAgentErrors();
    }
}
