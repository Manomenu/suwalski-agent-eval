using System.Collections.Concurrent;

namespace Suwageval.Server.Database;

// Why: grate never deletes the folders in the OS temp directory it copies its scripts to and names them only in its log, so they are read from there and removed
internal sealed class GrateTemporaryFolders(string scriptsDirectory) : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> folders = new();

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
