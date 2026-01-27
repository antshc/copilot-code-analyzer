using System.Diagnostics;
using System.Text;

namespace ReviewApp.Infrastructure;

public class ProcessRunner : IProcessRunner
{
    private readonly IProcessOutputSink _defaultSink;

    public ProcessRunner(IProcessOutputSink? defaultSink = null)
        => this._defaultSink = defaultSink ?? new ConsoleProcessOutputSink();

    // Executes an external process and streams output through the configured sink.
    public async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        IDictionary<string, string?>? environmentVariables = null,
        IProcessOutputSink? outputSink = null,
        CancellationToken cancellationToken = default)
    {
        var sink = outputSink ?? _defaultSink;

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (environmentVariables is not null)
        {
            foreach (var kvp in environmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();

        var stdoutTask = ReadLinesAsync(process.StandardOutput, sink.WriteStandardOut, stdoutBuffer, cancellationToken);
        var stderrTask = ReadLinesAsync(process.StandardError, sink.WriteStandardError, stderrBuffer, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new CommandResult(process.ExitCode, stdoutBuffer.ToString(), stderrBuffer.ToString());
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        Action<string> writeLine,
        StringBuilder buffer,
        CancellationToken cancellationToken)
    {
        // Reads lines until the stream ends; useful for long-running commands.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                return;
            }

            buffer.AppendLine(line);
            writeLine(line);
        }
    }
}

public interface IProcessOutputSink
{
    // Handles a single stdout line from a process.
    void WriteStandardOut(string line);

    // Handles a single stderr line from a process.
    void WriteStandardError(string line);
}

public interface IProcessRunner
{
    // Runs a process with optional environment variables and returns captured output.
    Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        IDictionary<string, string?>? environmentVariables = null,
        IProcessOutputSink? outputSink = null,
        CancellationToken cancellationToken = default);
}

public record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    // Indicates whether the process completed successfully.
    public bool IsSuccess => ExitCode == 0;
}

public sealed class ConsoleProcessOutputSink : IProcessOutputSink
{
    // Writes stdout to the console immediately to mirror CLI feedback.
    public void WriteStandardOut(string line) => Console.WriteLine(line);

    // Writes stderr to the console immediately to mirror CLI feedback.
    public void WriteStandardError(string line) => Console.Error.WriteLine(line);
}

public sealed class NullProcessOutputSink : IProcessOutputSink
{
    // Ignores stdout; used for tests.
    public void WriteStandardOut(string line)
    {
    }

    // Ignores stderr; used for tests.
    public void WriteStandardError(string line)
    {
    }
}
