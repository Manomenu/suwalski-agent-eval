using Microsoft.Extensions.Options;

namespace Suwageval.Server.Database;

internal static class DatabaseServiceCollectionExtensions
{
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
