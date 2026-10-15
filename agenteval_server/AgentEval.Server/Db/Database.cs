using Npgsql;

namespace AgentEval.Server.Db;

/// <summary>PostgreSQL: how the server gets to it.</summary>
internal static class Database
{
    /// <summary>
    /// The pool as a singleton, and the migrations at start-up: without the database there is nothing to
    /// serve, so the server fails to start with a clear error rather than failing every request later.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, string databaseUrl, bool migrateOnStart)
    {
        services.AddSingleton(_ => Pool.Create(databaseUrl));
        if (migrateOnStart)
        {
            services.AddHostedService<DatabaseStartup>();
        }
        return services;
    }

    /// <summary>
    /// <c>postgresql://user:password@host:port/database</c> as an Npgsql connection string. The platform
    /// repo hands the server a URL (the same DATABASE_URL every project reads), Npgsql wants key=value.
    /// </summary>
    public static string ToConnectionString(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("postgresql" or "postgres"))
        {
            throw new FormatException("DATABASE_URL must look like postgresql://user:password@host:port/database");
        }
        // Parameters (sslmode=…) would be silently dropped here; refuse them until something needs one.
        if (url.Query.Length > 0)
        {
            throw new FormatException($"DATABASE_URL parameters are not supported yet: {url.Query}");
        }
        var credentials = url.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.Port > 0 ? url.Port : 5432,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            Database = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/')),
            // Waiting longer for a database that is not there only delays the error.
            Timeout = 10,
        }.ConnectionString;
    }
}
