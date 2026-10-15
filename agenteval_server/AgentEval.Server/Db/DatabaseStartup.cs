namespace AgentEval.Server.Db;

/// <summary>Applies the missing migrations when the server starts — before it takes any request.</summary>
internal sealed partial class DatabaseStartup(Pool pool, ILogger<DatabaseStartup> log) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (
            var name in await Migrations.MigrateAsync(connection, Migrations.DefaultDirectory, cancellationToken).ConfigureAwait(false)
        )
        {
            Applied(log, name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "applied migration {Name}")]
    private static partial void Applied(ILogger logger, string name);
}
