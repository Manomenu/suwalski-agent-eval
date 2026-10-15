namespace AgentEval.Server.Db;

/// <summary>A migration this database already ran no longer matches its file.</summary>
internal sealed class MigrationChangedException(string message) : Exception(message);
