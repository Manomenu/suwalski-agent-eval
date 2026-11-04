namespace Suwageval.Cli.Evaluation.Sweep;

internal delegate Task<int> ShellCommand(string commandLine, TextWriter output, CancellationToken cancellationToken);
