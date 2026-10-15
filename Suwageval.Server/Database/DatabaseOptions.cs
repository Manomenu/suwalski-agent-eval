namespace Suwageval.Server.Database;

/// <summary>Where the database is: the <c>ConnectionStrings:Default</c> setting, an Npgsql connection string.</summary>
internal sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}
