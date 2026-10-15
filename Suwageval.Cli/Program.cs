using Suwageval.Cli;

// The entry point only hands over the process's streams; what the CLI is lives in CliApp.cs.
return CliApp.Run(args, Console.In, Console.Out, Console.Error);
