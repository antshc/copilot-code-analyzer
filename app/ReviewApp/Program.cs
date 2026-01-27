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
}
