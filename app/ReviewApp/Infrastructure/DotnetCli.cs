namespace ReviewApp.Infrastructure;

public interface IDotnetCli
{
    // Builds the given project with analyzer settings enabled.
    Task<CommandResult> BuildWithAnalyzersAsync(string projectPath, CancellationToken cancellationToken = default);
}

public class DotnetCli : IDotnetCli
{
    private const string AnalyzerArgs = "-p:EnableNETAnalyzers=true -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true -p:AnalysisLevel=latest -p:TreatWarningsAsErrors=false -p:GenerateDocumentationFile=true";
    private readonly IProcessRunner _processRunner;

    public DotnetCli(IProcessRunner processRunner) => _processRunner = processRunner;

    // Invokes dotnet build with analyzer flags and returns the raw command result.
    public Task<CommandResult> BuildWithAnalyzersAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var args = $"build {projectPath} {AnalyzerArgs}";
        return _processRunner.RunAsync("dotnet", args, cancellationToken: cancellationToken);
    }
}
