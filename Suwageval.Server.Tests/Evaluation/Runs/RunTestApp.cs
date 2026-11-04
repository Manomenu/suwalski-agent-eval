using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Monitoring.Checking;
using Suwageval.Server.Monitoring.Conversations;

namespace Suwageval.Server.Tests.Evaluation.Runs;

// Why: the workers are left out because they would take the queued runs of tests sharing the database or close their conversations
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
                foreach (
                    var worker in services
                        .Where(descriptor =>
                            descriptor.ImplementationType == typeof(RunWorker)
                            || descriptor.ImplementationType == typeof(ConversationCloser)
                            || descriptor.ImplementationType == typeof(MonitoringWorker)
                        )
                        .ToList()
                )
                {
                    services.Remove(worker);
                }
            });
        });
}
