using ReviewApp.Core.Abstractions;
using ReviewApp.Core.Abstractions.Analyzers;
using ReviewApp.Core.Analyzers;

namespace ReviewApp.Core;

internal class Reviewer
{
    private readonly IGitClient _gitCli;
    private readonly ICopilotClient _copilotCli;
    private readonly IAnalyzerRunner _analyzerRunner;
    private readonly IBranchState _branchState;
    private readonly AppConfig _appConfig;
    private readonly IChangeDetector _changesDetector;
    private readonly IDiffCollector _diffCollector;
    private readonly IFileSystemService _fileSystem;
    private readonly OutputArtifacts _artifacts;

    public Reviewer(
        IGitClient gitCli,
        ICopilotClient copilotCli,
        IAnalyzerRunner analyzerRunner,
        IBranchState branchState,
        AppConfig appConfig,
        IChangeDetector changesDetector,
        IDiffCollector diffCollector,
        IFileSystemService fileSystem,
        OutputArtifacts artifacts)
    {
        _gitCli = gitCli;
        _copilotCli = copilotCli;
        _analyzerRunner = analyzerRunner;
        _branchState = branchState;
        _appConfig = appConfig;
        _changesDetector = changesDetector;
        _diffCollector = diffCollector;
        _fileSystem = fileSystem;
        _artifacts = artifacts;
    }

    public async Task PerformReviewAsync(CancellationToken cancellationToken = default)
    {
        CleanupReport(_fileSystem, _artifacts.ReportOut);

        await CheckoutReviewBranch(_branchState, _appConfig, cancellationToken);
        IReadOnlyList<string> changedFiles = await ReadChangedFiles(_changesDetector, cancellationToken);

        await RunAnalyzersIfEnabled(_appConfig, _copilotCli, _analyzerRunner, changedFiles, cancellationToken);

        CleanupChanges(_fileSystem, _artifacts.OutputDir);
        await PrepareReviewChanges(_diffCollector, changedFiles, cancellationToken);
        await RunPrompt(_copilotCli, _appConfig.ReviewPrompt, _appConfig.CopilotToken, cancellationToken);
        CleanupChanges(_fileSystem, _artifacts.OutputDir);
        await RestoreBranchState(_gitCli, _appConfig.BranchName, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadChangedFiles(IChangeDetector changesDetector, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> changedFiles = await changesDetector.GetChangedCSharpFilesAsync(cancellationToken);

        return changedFiles;
    }

    private static async Task RunAnalyzersIfEnabled(AppConfig appConfig, ICopilotClient copilotCli, IAnalyzerRunner analyzerManager, IReadOnlyList<string> changedFiles, CancellationToken cancellationToken)
    {
        if (appConfig.AnalyzersEnabled)
        {
            await analyzerManager.RunAsync(changedFiles, cancellationToken);
            await RunPrompt(copilotCli, appConfig.CodeAnalysisReportPrompt, appConfig.CopilotToken, cancellationToken);
        }
        else
        {
            Console.WriteLine("Skipping analyzer");
        }
    }

    private static async Task CheckoutReviewBranch(IBranchState branchState, AppConfig appConfig, CancellationToken cancellationToken) =>
        await branchState.SetReviewBranch(appConfig.BaseBranchName, appConfig.BranchName, cancellationToken);

    private static void CleanupReport(IFileSystemService fileSystem, string reportOut) => fileSystem.RecreateDirectory(reportOut);

    private static async Task PrepareReviewChanges(IDiffCollector diffCollector, IReadOnlyList<string> changedFiles, CancellationToken cancellationToken) =>
        await diffCollector.CollectAsync(changedFiles, cancellationToken);

    private static async Task RunPrompt(ICopilotClient copilotCli, string reviewPrompt, string copilotToken, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Perform copilot review: {Environment.NewLine} {reviewPrompt}");
        await copilotCli.RunReviewAsync(reviewPrompt, copilotToken, cancellationToken);
    }

    private static void CleanupChanges(IFileSystemService fileSystem, string outputDir) => fileSystem.RecreateDirectory(outputDir);

    private static async Task RestoreBranchState(IGitClient client, string branchName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Restoring branch state for {branchName}");
        await client.CheckoutBranchResetAsync(branchName, cancellationToken);
    }
}
