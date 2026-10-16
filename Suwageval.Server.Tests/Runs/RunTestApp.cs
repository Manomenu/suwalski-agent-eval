using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

/// <summary>The server in process, on the test database, without its worker: the tests share one database, and a worker would take their queued runs.</summary>
internal static class RunTestApp
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The caller owns the factory and disposes it."
    )]
    public static WebApplicationFactory<Program> Create() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Default", TestDatabase.ConnectionString);
            host.ConfigureServices(services =>
            {
                foreach (var worker in services.Where(descriptor => descriptor.ImplementationType == typeof(RunWorker)).ToList())
                {
                    services.Remove(worker);
                }
            });
        });
}
