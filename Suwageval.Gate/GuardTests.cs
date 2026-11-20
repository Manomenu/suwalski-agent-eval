using System.Reflection;
using Suwgate.Architecture.Profiles;
using Suwgate.Architecture.Sources;
using Suwgate.Database.Profiles;

namespace Suwageval.Gate;

public sealed class GuardTests
{
    internal static readonly RepositoryFiles Repository = RepositoryFiles.Find();

    private static readonly Assembly Server = Assembly.Load("Suwageval.Server");

    [Fact]
    public void Every_guard_of_suwgate_passes() =>
        SuwgateGuards.VerifyAll(
            new GuardInputs
            {
                Production =
                [
                    Server,
                    Assembly.Load("suwageval"),
                    Assembly.Load("Suwageval.Contracts"),
                    Assembly.Load("Suwageval.AgentSdk"),
                ],
                Tests = [typeof(GuardTests).Assembly],
                Server = Server.GetType("Suwageval.Server.Api.ServerApp", true),
                EntryPoints = ["Suwageval.Server.LiveCheck.JudgeLiveCheck", "Suwageval.Server.Database.RunGroupSql"],
                ServerTypes = ["WebApplicationFactory", "RunTestApp"],
            },
            new DatabaseProfile()
        );
}
