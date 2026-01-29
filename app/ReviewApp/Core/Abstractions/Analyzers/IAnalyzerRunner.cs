namespace ReviewApp.Core.Abstractions.Analyzers;

public interface IAnalyzerRunner
{
    Task RunAsync(IReadOnlyList<string> changedFiles, CancellationToken cancellationToken = default);
}
