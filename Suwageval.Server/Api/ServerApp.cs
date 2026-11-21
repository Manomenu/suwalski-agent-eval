using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts;
using Suwageval.Server.Agents;
using Suwageval.Server.Agents.Versions;
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
using Suwbackkit.Postgres;
using Suwbackkit.Server;

namespace Suwageval.Server.Api;

internal static class ServerApp
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServerDefaults();
        builder.Services.AddDatabase(builder.Configuration, migrateOnStart: !ServerDefaults.WritingOpenApiDocument);
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
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddRuns(builder.Configuration, startWorker: !ServerDefaults.WritingOpenApiDocument);
        builder.Services.AddConversations(startCloser: !ServerDefaults.WritingOpenApiDocument);
        builder.Services.AddSingleton<MonitoringCheckStore>();
        builder.Services.AddChecking(startWorker: !ServerDefaults.WritingOpenApiDocument);

        var app = builder.Build();
        app.MapServerDefaults();
        var build = ServerVersion.Of(typeof(ServerApp).Assembly);
        app.Use(
            (context, next) =>
            {
                context.Response.Headers[ServerVersion.Header] = build;
                return next(context);
            }
        );
        app.MapOpenApi();

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
