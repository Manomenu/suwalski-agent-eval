using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Suwageval.Server.Database;

/// <summary>
/// The connection pool every part of the server uses (and the tests, so they test the same thing). The only
/// way to a connection: features take a <see cref="Pool"/>, never the data source underneath.
/// </summary>
internal sealed class Pool(NpgsqlDataSource dataSource, int maxSize) : IAsyncDisposable
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The pool owns the data source and disposes it."
    )]
    public static Pool Create(string connectionString, int maxSize = 5)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxSize };
        return new(NpgsqlDataSource.Create(builder), maxSize);
    }

    /// <summary>
    /// A pooled connection, tried before it is handed out. Without the check, the first request after the
    /// database restarted (a new image, a node reboot) gets a dead connection from the pool and fails with
    /// 57P01 — a 500 for whoever opens the page first. A dead one is dropped by Npgsql and the next is tried;
    /// at worst every idle connection in the pool is dead, so that many tries plus a fresh one.
    /// </summary>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var check = new NpgsqlCommand("SELECT 1", connection);
                await check.ExecuteNonQueryAsync(cancellationToken);
                return connection;
            }
            catch (NpgsqlException) when (attempt < maxSize && connection.State != System.Data.ConnectionState.Open)
            {
                await connection.DisposeAsync();
            }
        }
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
