using System.Reflection;
using Suwgate.Architecture.Profiles;
using Suwgate.Architecture.Sources;
using Suwgate.Database.Profiles;

namespace Suwageval.Gate;

public sealed class GuardTests
{
    internal static readonly RepositoryFiles Repository = RepositoryFiles.Find();

    internal static readonly Assembly Server = Assembly.Load("Suwageval.Server");

    internal static readonly Assembly Cli = Assembly.Load("suwageval");

    [Fact]
    public void Every_guard_of_suwgate_passes() =>
        SuwgateGuards.VerifyAll(
            new GuardInputs
            {
                Production = [Server, Cli, Assembly.Load("Suwageval.Contracts"), Assembly.Load("Suwageval.AgentSdk")],
                Tests = [typeof(GuardTests).Assembly],
                Server = Server.GetType("Suwageval.Server.Api.ServerApp", true),
                EntryPoints = ["Suwageval.Server.LiveCheck.JudgeLiveCheck", "Suwageval.Server.Database.RunGroupSql"],
            },
            new DatabaseProfile()
        );
}
