using ReviewApp.Core;
using ReviewApp.Infrastructure;

namespace ReviewApp;

public class Program
{
    private static readonly string ReviewPromptUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md";
    private static readonly string MinimalEditorConfigUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/rules/minimal.editorconfig";

    public static async Task Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var processRunner = new ProcessRunner();
        var gitClient = new GitClient(processRunner);
        var repoRoot = await gitClient.GetRepoRootAsync(cancellationToken);
        var artifacts = new OutputArtifacts(repoRoot);

        var appConfig = await AppConfigLoader.LoadAsync(args, cancellationToken);
        var downloader = new CurlDownloader(processRunner);
        var branchState = new BranchState(gitClient);
        var fileSystem = new FileSystemService();
        var dotnetCli = new DotnetCli(processRunner);
        var copilotCli = new CopilotClient(processRunner);
        var projectLocator = new ProjectLocator(artifacts.RepoRootDirectory);
        var changesDetector = new ChangeDetector(gitClient);
        var diffCollector = new DiffCollector(gitClient, fileSystem, artifacts.OutputDir);

        var editorConfigManager = new EditorConfigManager(
            fileSystem,
            downloader,
            artifacts.EditorConfigPath,
            artifacts.EditorConfigBackupPath,
            MinimalEditorConfigUrl);

        Console.WriteLine("Starting automated review workflow");

        CleanupReport(fileSystem, artifacts.ReportOut);

        var analyzerManager = new AnalyzerRunner(dotnetCli, editorConfigManager, fileSystem, projectLocator, artifacts.ReportOut);

        await CheckoutReviewBranch(branchState, appConfig, cancellationToken);
        IReadOnlyList<string> changedFiles = await ReadChangedFiles(changesDetector, cancellationToken);

        await RunAnalyzersIfEnabled(appConfig, analyzerManager, changedFiles, cancellationToken);

        CleanupChanges(fileSystem, artifacts.OutputDir);
        await PrepareReviewChanges(diffCollector, changedFiles, cancellationToken);
        await PerformChangesReview(artifacts.OutputDir, artifacts.ReportOut, downloader, copilotCli, appConfig, cancellationToken);
        CleanupChanges(fileSystem, artifacts.OutputDir);
        await RestoreBranchState(gitClient, appConfig.BranchName, cancellationToken);

        Console.WriteLine("Review workflow completed");
    }

    private static async Task<IReadOnlyList<string>> ReadChangedFiles(ChangeDetector changesDetector, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> changedFiles = await changesDetector.GetChangedCSharpFilesAsync(cancellationToken);

        return changedFiles;
    }

    private static async Task RunAnalyzersIfEnabled(AppConfig appConfig, AnalyzerRunner analyzerManager, IReadOnlyList<string> changedFiles, CancellationToken cancellationToken)
    {
        if (appConfig.AnalyzersEnabled)
        {
            await analyzerManager.RunAsync(changedFiles, cancellationToken);
        }
        else
        {
            Console.WriteLine("Skipping analyzer");
        }
    }

    private static async Task CheckoutReviewBranch(BranchState branchState, AppConfig appConfig, CancellationToken cancellationToken) =>
        await branchState.SetReviewBranch(appConfig.BaseBranchName, appConfig.BranchName, cancellationToken);

    private static void CleanupReport(FileSystemService fileSystem, string reportOut) => fileSystem.RecreateDirectory(reportOut);

    private static async Task PrepareReviewChanges(DiffCollector diffCollector, IReadOnlyList<string> changedFiles, CancellationToken cancellationToken) =>
        await diffCollector.CollectAsync(changedFiles, cancellationToken);

    private static async Task PerformChangesReview(string outputDir, string reportOut, CurlDownloader downloader, CopilotClient copilotCli, AppConfig appConfig, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Downloading prompt from {ReviewPromptUrl}");
        string reviewPrompt = await downloader.DownloadStringAsync(ReviewPromptUrl, cancellationToken);
        await copilotCli.RunReviewAsync(reviewPrompt, outputDir, reportOut, appConfig.CopilotToken, cancellationToken);
    }

    private static void CleanupChanges(FileSystemService fileSystem, string outputDir) => fileSystem.RecreateDirectory(outputDir);

    private static async Task RestoreBranchState(GitClient client, string branchName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Restoring branch state for {branchName}");
        await client.CheckoutBranchResetAsync(branchName, cancellationToken);
    }
}
