using System.Reflection;

namespace Suwageval.Gate.Design;

public sealed class SubtractTests
{
    // Why: the top-level Program's generated Main is invisible to the architecture reader, and a const leaves no trace in compiled code
    private static readonly string[] EntryPoints =
    [
        "Suwageval.Server.Program",
        "Suwageval.Server.Api.ServerApp",
        "Suwageval.Server.LiveCheck.JudgeLiveCheck",
        "Suwageval.Server.Database.RunGroupSql",
    ];

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Nothing_is_marked_obsolete()
    {
        var obsolete = SolutionTypes
            .All.SelectMany(type => new MemberInfo[] { type }.Concat(type.GetMembers(AllMembers)))
            .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is not null)
            // Why: the compiler marks the constructors of a type with required members obsolete by itself, for older compilers
            .Where(member => member.GetCustomAttribute<System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute>() is null)
            .Select(member => member is Type type ? type.FullName : $"{member.DeclaringType?.FullName}.{member.Name}")
            .Order()
            .ToList();

        Assert.True(obsolete.Count == 0, $"migrate the callers and delete it in this change, no shims: {string.Join(", ", obsolete)}");
    }

    [Fact]
    public void Every_non_public_type_of_the_server_is_used_by_another_type()
    {
        var unused = ServerContractsTests
            .Server.Types.Where(type => !type.Name.Contains('<', StringComparison.Ordinal))
            .Where(type => type.Visibility != ArchUnitNET.Domain.Visibility.Public)
            .Where(type => type.Namespace.FullName.StartsWith("Suwageval.Server", StringComparison.Ordinal))
            .Where(type => !EntryPoints.Contains(type.FullName))
            .Where(type =>
                !type
                    .BackwardsDependencies.Select(link => link.Origin)
                    .Any(user => user != type && !user.FullName.StartsWith(type.FullName + "+", StringComparison.Ordinal))
            )
            .Select(type => type.FullName)
            .Order()
            .ToList();

        Assert.True(
            unused.Count == 0,
            $"unused type: delete it (or add the entry point to EntryPoints in SubtractTests.cs): {string.Join(", ", unused)}"
        );
    }
}
