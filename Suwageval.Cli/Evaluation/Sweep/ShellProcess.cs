using System.ComponentModel;
using System.Diagnostics;

namespace Suwageval.Cli.Evaluation.Sweep;

/// <summary>The real <see cref="ShellCommand"/>: the line goes to <c>sh -c</c> (<c>cmd /c</c> on Windows), in the current folder.</summary>
internal static class ShellProcess
{
    /// <summary>The exit code <see cref="RunAsync"/> gives a command that could not be started.</summary>
    public const int NotStarted = 127;

    public static async Task<int> RunAsync(string commandLine, TextWriter output, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"$ {commandLine}");
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (OperatingSystem.IsWindows())
        {
            start.Arguments = "/c " + commandLine;
        }
        else
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(commandLine);
        }
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new Win32Exception("The process did not start.");
        }
        catch (Win32Exception error)
        {
            await output.WriteLineAsync($"Could not start the command: {error.Message}");
            return NotStarted;
        }
        using (process)
        {
            var shared = TextWriter.Synchronized(output);
            process.OutputDataReceived += (_, line) => Write(shared, line.Data);
            process.ErrorDataReceived += (_, line) => Write(shared, line.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try
            {
                await process.WaitForExitAsync(cancellationToken);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
        }
    }

    private static void Write(TextWriter output, string? line)
    {
        if (line is not null)
        {
            output.WriteLine(line);
        }
    }
}
