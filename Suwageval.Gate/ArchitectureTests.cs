using System.Reflection;
using Suwgate.Architecture.Build;
using Suwgate.Architecture.Code;
using Suwgate.Architecture.Design;
using Suwgate.Architecture.Sources;
using Suwgate.Architecture.Verification;
using Suwgate.Database.Migrations;

namespace Suwageval.Gate;

public sealed class ArchitectureTests
{
    internal static readonly RepositoryFiles Repository = RepositoryFiles.Find();

    internal static readonly LayersFile LayersFile = LayersFile.Read(Repository, "layers.json");

    internal static readonly Assembly Server = Assembly.Load("Suwageval.Server");

    internal static readonly Assembly Cli = Assembly.Load("suwageval");

    private static readonly Assembly[] Assemblies =
    [
        Server,
        Cli,
        Assembly.Load("Suwageval.Contracts"),
        Assembly.Load("Suwageval.AgentSdk"),
        typeof(ArchitectureTests).Assembly,
    ];

    private static readonly string[] ServerEntryPoints =
    [
        "Suwageval.Server.Program",
        "Suwageval.Server.Api.ServerApp",
        "Suwageval.Server.LiveCheck.JudgeLiveCheck",
        "Suwageval.Server.Database.RunGroupSql",
    ];

    private static IReadOnlyList<SourceFile> ProductionSources() =>
        [.. Repository.CSharpSources().Where(file => !file.Path.Split('/')[0].EndsWith(".Tests", StringComparison.Ordinal))];

    [Fact]
    public void Layers() => Guards.Verify(new Layers(LayersFile, Server));

    [Fact]
    public void Endpoints() => Guards.Verify(new Endpoints(Server, "Suwageval.Server.Api", "Endpoints"));

    [Fact]
    public void Project_references() => Guards.Verify(new ProjectReferences(Repository, "Suwageval.slnx", LayersFile));

    [Fact]
    public void Every_project_guarded() => Guards.Verify(new EveryProjectGuarded(LayersFile, Assemblies));

    [Fact]
    public void Interfaces_have_two_implementations() => Guards.Verify(InterfacesHaveTwoImplementations.Of(Assemblies));

    [Fact]
    public void Nothing_obsolete() => Guards.Verify(new NothingObsolete(Assemblies));

    [Fact]
    public void No_unused_types() => Guards.Verify(new NoUnusedTypes([Server], ServerEntryPoints));

    [Fact]
    public void Forbidden_type_names() => Guards.Verify(ForbiddenTypeNames.Of(Assemblies));

    [Fact]
    public void Unique_type_names() => Guards.Verify(UniqueTypeNames.Of(Assemblies));

    [Fact]
    public void One_type_per_file() => Guards.Verify(new OneTypePerFile(Repository.CSharpSources()));

    [Fact]
    public void No_escape_hatches() => Guards.Verify(new NoEscapeHatches(ProductionSources()));

    [Fact]
    public void Build_settings() => Guards.Verify(new BuildSettings(Repository));

    [Fact]
    public void Migration_script_names() =>
        Guards.Verify(new MigrationScriptNames(Path.Combine(Repository.Root, "Suwageval.Server/Database/Migrations/up")));
}
