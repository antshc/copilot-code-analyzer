using ReviewApp.Infrastructure;

namespace ReviewApp.Core.Abstractions;

public interface IDotnetCli
{
    // Builds the given project with analyzer settings enabled.
    Task<CommandResult> BuildWithAnalyzersAsync(string projectPath, CancellationToken cancellationToken = default);
}
