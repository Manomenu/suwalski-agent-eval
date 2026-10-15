using AgentEval.Server.Database;
using Npgsql;

namespace AgentEval.Server.Tests.Database;

public sealed class PoolTests(TestDatabase database)
{
    [Fact]
    public async Task The_pool_replaces_a_connection_the_database_killed()
    {
        // What a database restart (e.g. a new image) does to every idle connection in the pool. The
        // server's pool must hand out a working one, not the dead one — or the next request is a 500.
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
