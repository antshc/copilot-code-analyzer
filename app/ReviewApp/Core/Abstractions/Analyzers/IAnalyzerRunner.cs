namespace ReviewApp.Core.Abstractions.Analyzers;

public interface IAnalyzerRunner
{
    Task RunAsync(IReadOnlyList<string> changedFiles, bool appConfigCodeAnalysisReportEnabled, CancellationToken cancellationToken = default);
}
