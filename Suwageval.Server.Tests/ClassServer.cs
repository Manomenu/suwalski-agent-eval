using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests;

public sealed class ClassServer : IAsyncDisposable
{
    public ClassServer()
    {
        App = RunTestApp.Create(Time);
    }

    internal FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    internal WebApplicationFactory<Program> App { get; }

    public ValueTask DisposeAsync() => App.DisposeAsync();
}
