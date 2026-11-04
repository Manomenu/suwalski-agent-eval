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
using Suwageval.Server.Monitoring.Checking;
using Suwageval.Server.Monitoring.Checks;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.ScenarioDrafts;

namespace Suwageval.Server.Api;

// Why: routes have no /api prefix because the edge (vite proxy, nginx) strips it before the request lands here
internal static class ServerApp
{
    // Why: the build writes the OpenAPI document by starting the app inside its GetDocument tool, so the database must not be touched
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
            // Why: an unset field is absent from a response, and this is not in ContractJson.Options because that also serializes what is hashed
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddRuns(builder.Configuration, startWorker: !WritingOpenApiDocument);
        builder.Services.AddConversations(startCloser: !WritingOpenApiDocument);
        builder.Services.AddSingleton<MonitoringCheckStore>();
        builder.Services.AddChecking(startWorker: !WritingOpenApiDocument);
        builder.Services.AddOpenApi();

        var app = builder.Build();
        // Why: every answer says which build sent it, because the CLI names it when it cannot read an answer
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

        // Why: touches nothing outside the process, so it doubles as the cluster's liveness probe
        app.MapGet("/health", () => new Health("ok"));

        app.MapAgents();
        app.MapVersions();
        MapEvaluation(app.MapGroup("/eval"));
        var monitoring = app.MapGroup("/monitoring");
        monitoring.MapConversations();
        monitoring.MapMonitoringChecks();
        monitoring.MapChecking();
        monitoring.MapScenarioDrafts();
        return app;
    }

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
