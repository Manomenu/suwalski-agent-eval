using Microsoft.Extensions.Options;

namespace AgentEval.Server.Database;

/// <summary>PostgreSQL: how the server gets to it.</summary>
internal static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// The pool as a singleton, and the migrations at start-up: without the database there is nothing to
    /// serve, so the server fails to start with a clear error rather than failing every request later.
    /// With <paramref name="migrateOnStart"/> off (the build writing the OpenAPI document) the database is
    /// not touched.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, bool migrateOnStart)
    {
        var options = services
            .AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("Default") ?? "")
            .Validate(options => options.ConnectionString.Length > 0, "ConnectionStrings:Default is missing");
        services.AddSingleton(provider => Pool.Create(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        if (migrateOnStart)
        {
            options.ValidateOnStart();
            services.AddHostedService<DatabaseStartup>();
        }
        return services;
    }
}
