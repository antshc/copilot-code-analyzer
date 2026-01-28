using ReviewApp.Core;
using ReviewApp.Infrastructure;

namespace ReviewApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var reviewer = await Create(args, cancellationToken);

        Console.WriteLine("Starting automated review workflow");
        await reviewer.PerformReviewAsync(cancellationToken);
        Console.WriteLine("Review workflow completed");
    }

    private static async Task<Reviewer> Create(string[] args, CancellationToken cancellationToken)
    {
        var repoRoot = await GetRepoRoot(cancellationToken);
        var processRunner = new ProcessRunner(repoRoot);
        var gitClient = new GitClient(processRunner);
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
            appConfig.EditorConfig,
            artifacts.OriginalEditorConfigPath,
            artifacts.EditorConfigBackupPath);

        var analyzerRunner = new AnalyzerRunner(dotnetCli, editorConfigManager, fileSystem, projectLocator, artifacts.ReportOut);

        return new Reviewer(
            gitClient,
            copilotCli,
            analyzerRunner,
            branchState,
            appConfig,
            changesDetector,
            diffCollector,
            fileSystem,
            downloader,
            artifacts);
    }

    private static async Task<string> GetRepoRoot(CancellationToken cancellationToken)
    {
        var baseProcessRunner = new ProcessRunner();
        var baseGitClient = new GitClient(baseProcessRunner);
        var repoRoot = await baseGitClient.GetRepoRootAsync(cancellationToken);
        return repoRoot;
    }
}
