using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests;

// Why: starting the server costs about a second, so a test class starts it once; each test still works on agents of its own
public sealed class ClassServer : IAsyncDisposable
{
    internal WebApplicationFactory<Program> App { get; } = RunTestApp.Create();

    public ValueTask DisposeAsync() => App.DisposeAsync();
}
