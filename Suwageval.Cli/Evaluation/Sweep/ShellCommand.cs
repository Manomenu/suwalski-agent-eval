namespace Suwageval.Cli.Evaluation.Sweep;

/// <summary>Runs a command line of agent.yaml's <c>sweep:</c> section, its output going to <paramref name="output"/>; the exit code.</summary>
internal delegate Task<int> ShellCommand(string commandLine, TextWriter output, CancellationToken cancellationToken);
