using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests;

public sealed class ClassServer : IAsyncDisposable
{
    internal WebApplicationFactory<Program> App { get; } = RunTestApp.Create();

    public ValueTask DisposeAsync() => App.DisposeAsync();
}
