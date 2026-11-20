using Npgsql;
using Suwageval.Server.Database;

namespace Suwageval.Server.Tests.Database;

public sealed class PoolTests(TestDatabase database)
{
    [Fact]
    public async Task The_pool_replaces_a_connection_the_database_killed()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var pool = Pool.Create(TestDatabase.ConnectionString, maxSize: 1);
        int victim;
        await using (var connection = await pool.OpenAsync(cancellation))
        {
            victim = connection.ProcessID;
        }
        await using (var admin = await database.Pool.OpenAsync(cancellation))
        await using (var kill = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", admin))
        {
            kill.Parameters.AddWithValue("pid", victim);
            await kill.ExecuteNonQueryAsync(cancellation);
        }

        await using var fresh = await pool.OpenAsync(cancellation);
        await using var probe = new NpgsqlCommand("SELECT 1", fresh);

        Assert.Equal(1, await probe.ExecuteScalarAsync(cancellation));
        Assert.NotEqual(victim, fresh.ProcessID);
    }
}
