using AgentEval.Server.Db;
using Npgsql;

namespace AgentEval.Server.Tests.Db;

public sealed class DatabaseTests
{
    [Fact]
    public void A_url_becomes_a_connection_string()
    {
        var connection = new NpgsqlConnectionStringBuilder(Database.ToConnectionString("postgresql://ewa:p%40ss@db.local:5999/evals"));

        Assert.Equal(
            ("db.local", 5999, "ewa", "p@ss", "evals"),
            (connection.Host, connection.Port, connection.Username, connection.Password, connection.Database)
        );
    }

    [Fact]
    public void The_port_defaults_to_postgres_own() =>
        Assert.Equal(5432, new NpgsqlConnectionStringBuilder(Database.ToConnectionString("postgres://u:p@db/evals")).Port);

    [Theory]
    [InlineData("Host=db;Database=evals")]
    [InlineData("mysql://u:p@db/evals")]
    [InlineData("postgresql://u:p@db/evals?sslmode=require")]
    public void Anything_else_is_refused(string url) => Assert.Throws<FormatException>(() => Database.ToConnectionString(url));
}
