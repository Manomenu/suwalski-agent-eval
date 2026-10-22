extern alias Cli;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Server.Tests.Runs;
using CliApp = Cli::Suwageval.Cli.CliApp;

namespace Suwageval.Server.Tests.EndToEnd;

/// <summary>
/// AGENTS.md, section 5: "Anything the web can do, the CLI can do". Every route the server maps is listed below with the
/// <c>suwageval</c> command that covers it, or with the reason it is not for the CLI; a route that is not listed fails the gate, and
/// so does a command that does not exist. A new endpoint therefore comes with its command (or its stated reason) in the same change.
/// </summary>
public sealed class EveryEndpointHasACliCommandTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    /// <summary>A route and what covers it: the command (words as typed after <c>suwageval</c>), or why there is none.</summary>
    private sealed record Coverage(string? Command, string? Reason = null);

    private static Coverage Cli(string command) => new(command);

    private static Coverage NotForCli(string reason) => new(null, reason);

    private const string NoFrontDoor =
        "Neither the web nor the CLI uses it yet; the CLI gets a command when a front door needs it (AGENTS.md, section 0).";

    private static readonly Dictionary<string, Coverage> Routes = new()
    {
        ["GET /health"] = NotForCli("The liveness probe of the cluster; nobody asks it from the CLI."),
        ["GET /openapi/{documentName}.json"] = NotForCli("The OpenAPI document the web's types are generated from."),
        ["POST /agents"] = Cli("push"),
        ["GET /agents"] = Cli("agents"),
        ["GET /agents/{agent}"] = Cli("agent"),
        ["GET /agents/{agent}/deletion"] = Cli("delete-agent"),
        ["DELETE /agents/{agent}"] = Cli("delete-agent"),
        ["GET /agents/{agent}/versions"] = Cli("versions"),
        ["POST /agents/{agent}/versions/archive"] = Cli("versions archive"),
        ["GET /agents/{agent}/versions/diff"] = Cli("versions diff"),
        ["GET /agents/{agent}/versions/{versionId}"] = Cli("versions show"),
        ["GET /agents/{agent}/versions/{versionId}/deletion"] = Cli("versions delete"),
        ["DELETE /agents/{agent}/versions/{versionId}"] = Cli("versions delete"),
        ["PUT /agents/{agent}/scenarios"] = Cli("push"),
        ["GET /agents/{agent}/scenarios"] = Cli("scenarios"),
        ["GET /agents/{agent}/scenarios/{key}"] = Cli("scenario"),
        ["GET /agents/{agent}/scenarios/{key}/revisions"] = Cli("scenario"),
        ["DELETE /agents/{agent}/scenarios/{key}"] = NotForCli(NoFrontDoor),
        ["POST /agents/{agent}/scenarios/tags"] = NotForCli(NoFrontDoor),
        ["GET /agents/{agent}/tags"] = NotForCli(NoFrontDoor),
        ["PUT /agents/{agent}/checks"] = Cli("push"),
        ["GET /agents/{agent}/checks"] = Cli("scenarios"),
        ["DELETE /agents/{agent}/checks/{key}"] = NotForCli(NoFrontDoor),
        ["POST /runs"] = Cli("run"),
        ["GET /runs"] = Cli("runs"),
        ["GET /agents/{agent}/run-groups"] = Cli("runs"),
        ["POST /agents/{agent}/run-groups/archive"] = Cli("groups archive"),
        ["GET /agents/{agent}/run-groups/{name}/deletion"] = Cli("groups delete"),
        ["DELETE /agents/{agent}/run-groups/{name}"] = Cli("groups delete"),
        ["GET /runs/compare"] = Cli("compare"),
        ["GET /runs/{id}"] = Cli("results"),
        ["GET /runs/{id}/results"] = Cli("results"),
        ["POST /runs/{id}/rerun"] = Cli("rerun"),
        ["GET /runs/{id}/recheck"] = Cli("recheck"),
        ["POST /runs/{id}/recheck"] = Cli("recheck"),
        ["POST /runs/{id}/cancel"] = Cli("cancel"),
        ["GET /agents/{agent}/pass-rates"] = Cli("trend"),
        ["GET /agents/{agent}/unstable-checks"] = Cli("unstable"),
        ["GET /agents/{agent}/agent-errors"] = Cli("agent-errors"),
    };

    public ValueTask DisposeAsync() => app.DisposeAsync();

    /// <summary>Every route the server maps, as "METHOD pattern" (route constraints such as <c>:guid</c> left out).</summary>
    private List<string> Mapped()
    {
        _ = database.Pool;
        return
        [
            .. app
                .Services.GetRequiredService<EndpointDataSource>()
                .Endpoints.OfType<RouteEndpoint>()
                .SelectMany(endpoint =>
                    (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(method =>
                        $"{method} {StripConstraints(endpoint.RoutePattern.RawText ?? "")}"
                    )
                )
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string StripConstraints(string pattern) =>
        System.Text.RegularExpressions.Regex.Replace(
            pattern,
            @"\{(\w+):[^}]+\}",
            "{$1}",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1)
        );

    [Fact]
    public void Every_route_the_server_maps_is_covered_by_a_command_or_says_why_not()
    {
        var missing = Mapped().Where(route => !Routes.ContainsKey(route)).ToList();

        Assert.True(
            missing.Count == 0,
            $"These routes have no CLI command: {string.Join("; ", missing)}. Anything the web can do, the CLI can do (AGENTS.md, section 5): "
                + "add the `suwageval` command for it (with its tests, its layer in Suwageval.Cli.Tests/ContractsTests.cs, the plugin's SKILL.md and "
                + "docs/front-doors.md) and list the route in this table; or list it as NotForCli with the reason."
        );
    }

    [Fact]
    public void The_table_lists_no_route_the_server_does_not_map()
    {
        var mapped = Mapped().ToHashSet();

        foreach (var route in Routes.Keys)
        {
            Assert.Contains(route, mapped);
        }
    }

    [Fact]
    public void Every_command_the_table_names_exists()
    {
        var commands = Routes.Values.Where(coverage => coverage.Command is not null).Select(coverage => coverage.Command!).Distinct();

        foreach (var command in commands)
        {
            // Asked of the parent's help: an unknown word after a real command would just print that command's help.
            var words = command.Split(' ');
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exit = CliApp.Run(
                [.. words[..^1], "--help"],
                new StringReader(""),
                output,
                error,
                interrupt: TestContext.Current.CancellationToken
            );

            Assert.Equal(0, exit);
            var listed = output.ToString().Split(Environment.NewLine).Select(line => line.Trim()).ToList();
            Assert.True(
                listed.Exists(line => line.StartsWith(words[^1] + " ", StringComparison.Ordinal)),
                $"`suwageval {command}` is named in the table but is not a command."
            );
        }
    }
}
