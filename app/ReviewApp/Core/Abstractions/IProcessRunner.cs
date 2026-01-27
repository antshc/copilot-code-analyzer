using ReviewApp.Infrastructure;

namespace ReviewApp.Core.Abstractions;

public interface IProcessRunner
{
    string WorkingDirectory { get; }

    // Runs a process with optional environment variables and returns captured output.
    Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        IDictionary<string, string?>? environmentVariables = null,
        IProcessOutputSink? outputSink = null,
        CancellationToken cancellationToken = default);
}
