using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Suwageval.Server.Database;

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

    // Why: a connection is tried before it is handed out, or the first request after a database restart gets a dead one and fails with 57P01
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
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
