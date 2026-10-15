using System.CommandLine;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Validate;

/// <summary><c>suwageval validate [path]</c>: checks an agent's files; needs no server.</summary>
internal static class ValidateCommand
{
    private const string DefaultDir = "suwageval";
    private const string StdinName = "<stdin>";

    public static Command Create(TextReader input)
    {
        var path = new Argument<string>("path")
        {
            Description = "The suwageval folder, one scenario file, or '-' for one scenario read from stdin.",
            DefaultValueFactory = _ => DefaultDir,
        };
        var dir = new Option<string>("--dir")
        {
            Description = "The folder with agent.yaml, for a single scenario file or stdin.",
            DefaultValueFactory = _ => DefaultDir,
        };
        var json = new Option<bool>("--json") { Description = "Print { valid, problems: [{ file, path, message }] } instead of text." };

        var command = new Command(
            "validate",
            "Check the agent's files against the contract, without a server. Exit code 1 when anything is wrong."
        )
        {
            path,
            dir,
            json,
        };
        command.SetAction(parsed =>
        {
            var result = Run(parsed.GetRequiredValue(path), parsed.GetRequiredValue(dir), input);
            var output = parsed.InvocationConfiguration.Output;
            output.Write(parsed.GetValue(json) ? ValidationReport.Json(result) + Environment.NewLine : ValidationReport.Text(result));
            return result.IsValid ? 0 : 1;
        });
        return command;
    }

    private static ValidationResult Run(string path, string dir, TextReader input)
    {
        if (path == "-")
        {
            return Validator.Validate(AgentFile(dir), ReadIfExists(AgentFile(dir)), [new SourceText(StdinName, input.ReadToEnd())]);
        }
        if (File.Exists(path))
        {
            return Validator.Validate(AgentFile(dir), ReadIfExists(AgentFile(dir)), [new SourceText(path, File.ReadAllText(path))]);
        }
        if (Directory.Exists(path))
        {
            var scenarios = Path.Combine(path, "scenarios");
            var files = Directory.Exists(scenarios)
                ? Directory
                    .EnumerateFiles(scenarios, "*.yaml")
                    .Order(StringComparer.Ordinal)
                    .Select(f => new SourceText(f, File.ReadAllText(f)))
                : [];
            return Validator.Validate(AgentFile(path), ReadIfExists(AgentFile(path)), [.. files]);
        }
        return new ValidationResult([new Problem(path, "", "No such file or folder.", null, null)], 0, 0);
    }

    private static string AgentFile(string dir) => Path.Combine(dir, "agent.yaml");

    private static string? ReadIfExists(string file) => File.Exists(file) ? File.ReadAllText(file) : null;
}
