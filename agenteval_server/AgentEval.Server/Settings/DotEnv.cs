using Microsoft.Extensions.Configuration.Memory;

namespace AgentEval.Server.Settings;

/// <summary>
/// The layered .env files, read the way every project from solid-app-tpl reads them:
/// <c>.env.base -&gt; .env</c> at the solution root, then <c>agenteval_server/.env.base -&gt; agenteval_server/.env</c>.
/// Later files win, a missing file is skipped, and a real environment variable wins over all of them
/// (ServerApp puts this source under the others).
/// </summary>
internal static class DotEnv
{
    /// <summary>Set in the image, where the sources are not next to the binary.</summary>
    public const string SolutionRootVariable = "AGENTEVAL_SOLUTION_ROOT";

    public const string ProjectDirectory = "agenteval_server";

    /// <summary>The files, in loading order. <paramref name="contentRoot"/> is the C# project's directory in development.</summary>
    public static IReadOnlyList<string> Files(string contentRoot, string? solutionRoot)
    {
        // contentRoot = agenteval_server/AgentEval.Server, so two levels up is the solution root.
        solutionRoot ??= Path.GetFullPath(Path.Combine(contentRoot, "..", ".."));
        var project = Path.Combine(solutionRoot, ProjectDirectory);
        return
        [
            Path.Combine(solutionRoot, ".env.base"),
            Path.Combine(solutionRoot, ".env"),
            Path.Combine(project, ".env.base"),
            Path.Combine(project, ".env"),
        ];
    }

    public static IConfigurationSource Source(string contentRoot) =>
        new MemoryConfigurationSource { InitialData = Load(Files(contentRoot, Environment.GetEnvironmentVariable(SolutionRootVariable))) };

    public static Dictionary<string, string?> Load(IEnumerable<string> files)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(File.Exists))
        {
            foreach (var (key, value) in Parse(File.ReadAllText(file), file))
            {
                values[key] = value;
            }
        }
        return values;
    }

    /// <summary>
    /// <c>KEY=VALUE</c> per line; blank lines and lines starting with <c>#</c> are skipped. Comments sit on
    /// their own line (.env.example says why). A value in matching quotes loses them.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> Parse(string text, string source)
    {
        var number = 0;
        foreach (var raw in text.Split('\n'))
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                throw new FormatException($"{source}:{number}: expected KEY=VALUE, got \"{line}\"");
            }
            yield return new(line[..equals].Trim(), Unquote(line[(equals + 1)..].Trim()));
        }
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0] ? value[1..^1] : value;
}
