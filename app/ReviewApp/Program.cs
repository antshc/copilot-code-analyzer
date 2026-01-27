using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ReviewApp.Core;
using ReviewApp.Infrastructure;

namespace ReviewApp;

public class ReviewWorkflow
{
    private static readonly string ReviewPromptUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md";
    private static readonly string MinimalEditorConfigUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/rules/minimal.editorconfig";

    private static string? RepoRoot;
    private static string? ReportOut;
    private static string? OutputDir;
    private static string? EditorConfigPath;
    private static string? EditorConfigBackupPath;
    private static bool EditorConfigTempApplied = false;

    public static async Task Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var processRunner = new ProcessRunner();
        var gitClient = new GitClient(processRunner);
        RepoRoot = await gitClient.GetRepoRootAsync(cancellationToken);
        ReportOut = Path.Combine(RepoRoot, "report");
        OutputDir = Path.Combine(RepoRoot, "_changes");
        EditorConfigPath = Path.Combine(RepoRoot, ".editorconfig");
        EditorConfigBackupPath = Path.Combine(RepoRoot, ".editorconfig.backup");

        var appConfig = await AppConfigLoader.LoadAsync(args, cancellationToken);
        var downloader = new CurlDownloader(processRunner);
        var branchState = new BranchState(gitClient);
        var fileSystem = new FileSystemService();
        var dotnetCli = new DotnetCli(processRunner);
        var copilotCli = new CopilotClient(processRunner);
        var projectLocator = new ProjectLocator(RepoRoot);
        var changesDetector = new ChangeDetector(gitClient);
        var diffCollector = new DiffCollector(gitClient, fileSystem, OutputDir);

        var editorConfigManager = new EditorConfigManager(
            fileSystem,
            downloader,
            EditorConfigPath!,
            EditorConfigBackupPath!,
            MinimalEditorConfigUrl);

        var analyzerManager = new AnalyzerRunner(dotnetCli, editorConfigManager, fileSystem, projectLocator, ReportOut);
        Console.WriteLine("Starting automated review workflow");

        await CheckoutReviewBranch(branchState, appConfig, cancellationToken);
        CleanupReport(fileSystem);
        IReadOnlyList<string> changedFiles = await ReadChangedFiles(changesDetector, cancellationToken);

        await RunAnalyzersIfEnabled(appConfig, analyzerManager, changedFiles, cancellationToken);

        CleanupChanges(fileSystem);
        await PrepareReviewChanges(diffCollector, changedFiles, cancellationToken);
        await PerformChangesReview(downloader, cancellationToken, copilotCli, appConfig);
        CleanupChanges(fileSystem);
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

    private static async Task CheckoutReviewBranch(BranchState branchState, AppConfig appConfig, CancellationToken cancellationToken) => await branchState.SetReviewBranch(appConfig.BaseBranchName, appConfig.BranchName, cancellationToken);

    private static void CleanupReport(FileSystemService fileSystem) => fileSystem.RecreateDirectory(ReportOut);

    private static async Task PrepareReviewChanges(DiffCollector diffCollector, IReadOnlyList<string> changedFiles, CancellationToken cancellationToken) => await diffCollector.CollectAsync(changedFiles, cancellationToken);

    private static async Task PerformChangesReview(CurlDownloader downloader, CancellationToken cancellationToken, CopilotClient copilotCli, AppConfig appConfig)
    {
        Console.WriteLine($"Downloading prompt from {ReviewPromptUrl}");
        string reviewPrompt = await downloader.DownloadStringAsync(ReviewPromptUrl, cancellationToken);
        await copilotCli.RunReviewAsync(reviewPrompt, OutputDir , ReportOut, appConfig.CopilotToken, cancellationToken);
    }

    private static void CleanupChanges(FileSystemService fileSystem) => fileSystem.RecreateDirectory(OutputDir);

    private static async Task RestoreBranchState(GitClient client, string branchName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Restoring branch state for {branchName}");
        await client.CheckoutBranchResetAsync(branchName, cancellationToken);
    }
}
