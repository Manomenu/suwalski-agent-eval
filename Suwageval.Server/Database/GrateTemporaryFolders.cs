using System.Collections.Concurrent;

namespace Suwageval.Server.Database;

/// <summary>
/// The temporary folders grate writes its own scripts to and never deletes: on every migration it copies the scripts of its bookkeeping
/// tables into new folders (<c>Path.GetTempFileName</c>) and names them only in its log ("Looking in {UpFolder} for scripts to run.").
/// Read from that log, so <see cref="Migrator"/> can delete them — otherwise every start of the server leaves folders in the temporary
/// directory until it runs out of space.
/// </summary>
internal sealed class GrateTemporaryFolders(string scriptsDirectory) : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> folders = new();

    /// <summary>Deletes the folders seen so far — only those inside the temporary directory, never the migrations' own — and lists them.</summary>
    public IReadOnlyList<string> Delete()
    {
        var temporary = Path.GetFullPath(Path.GetTempPath());
        var scripts = Path.GetFullPath(scriptsDirectory);
        var deleted = folders
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .Where(folder => folder != scripts && folder.StartsWith(temporary, StringComparison.Ordinal) && Directory.Exists(folder))
            .ToList();
        foreach (var folder in deleted)
        {
            Directory.Delete(folder, recursive: true);
        }
        return deleted;
    }

    public ILogger CreateLogger(string categoryName) => this;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            return;
        }
        foreach (var (key, value) in values)
        {
            if (key == "UpFolder" && value?.ToString() is { Length: > 0 } folder)
            {
                folders.Enqueue(folder);
            }
        }
    }

    public void Dispose() { }
}
