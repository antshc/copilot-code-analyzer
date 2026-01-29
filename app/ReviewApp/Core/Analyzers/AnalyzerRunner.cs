using ReviewApp.Core.Abstractions;
using ReviewApp.Core.Abstractions.Analyzers;

namespace ReviewApp.Core.Analyzers;

public class AnalyzerRunner : IAnalyzerRunner
{
    private readonly AppConfig _appConfig;
    private readonly IDotnetCli _dotnetCli;
    private readonly ICopilotClient _copilotClient;
    private readonly EditorConfigManager _editorConfigManager;
    private readonly IFileSystemService _fileSystemService;
    private readonly ProjectLocator _projectLocator;
    private readonly string _reportDirectory;

    public AnalyzerRunner(
        AppConfig appConfig,
        IDotnetCli dotnetCli,
        ICopilotClient copilotClient,
        EditorConfigManager editorConfigManager,
        IFileSystemService fileSystemService,
        ProjectLocator projectLocator,
        string reportDirectory)
    {
        _appConfig = appConfig;
        _dotnetCli = dotnetCli;
        _copilotClient = copilotClient;
        _editorConfigManager = editorConfigManager;
        _fileSystemService = fileSystemService;
        _projectLocator = projectLocator;
        _reportDirectory = reportDirectory;
    }

    // Runs analyzer-enabled builds for projects containing changed files and writes diagnostics.
    public async Task RunAsync(IReadOnlyList<string> changedFiles, CancellationToken cancellationToken = default)
    {
        if (!changedFiles.Any())
        {
            return;
        }

        var projectToFiles = MapProjectsToFiles(changedFiles);

        await _editorConfigManager.ApplyMinimalConfigAsync(cancellationToken);

        try
        {
            foreach (var projectPath in projectToFiles.Keys)
            {
                Console.WriteLine($"Running analyzer-enabled build for {projectPath}");
                var result = await _dotnetCli.BuildWithAnalyzersAsync(projectPath, cancellationToken);

                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException($"dotnet build failed for {projectPath}: {result.StandardError.Trim()}");
                }

                await FilterAnalyzerRules(result.StandardOutput, projectPath, projectToFiles, cancellationToken);
            }

            await RunPrompt(_copilotClient, _appConfig.CodeAnalysisReportPrompt, _appConfig.CopilotToken, cancellationToken);
        }
        finally
        {
            _editorConfigManager.RestoreOriginal();
        }
    }

    private async Task FilterAnalyzerRules(string buildOutput, string projectPath, Dictionary<string, HashSet<string>> projectToFiles, CancellationToken cancellationToken)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var buildLogFileName = Path.Combine(_reportDirectory, $"{projectName}.log");
        await _fileSystemService.WriteFileAsync(buildLogFileName, buildOutput, cancellationToken);

        var fileNames = projectToFiles[projectPath].Select(Path.GetFileNameWithoutExtension).ToArray();

        var filteredLines = buildOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => fileNames.Any(line.Contains))
            .ToArray();

        var buildDiagFileName = Path.Combine(_reportDirectory, $"{projectName}.diag.log");
        await _fileSystemService.WriteFileAsync(buildDiagFileName, string.Join(Environment.NewLine, filteredLines), cancellationToken);
        _fileSystemService.DeleteFileIfExists(buildLogFileName);
    }

    private Dictionary<string, HashSet<string>> MapProjectsToFiles(IReadOnlyList<string> changedFiles)
    {
        // Groups changed files by their owning project to minimize build invocations.
        var projectToFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in changedFiles)
        {
            var projectPath = _projectLocator.FindProjectForFile(filePath);

            if (!projectToFiles.TryGetValue(projectPath, out var files))
            {
                files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                projectToFiles[projectPath] = files;
            }

            files.Add(filePath);
        }

        return projectToFiles;
    }

    private static async Task RunPrompt(ICopilotClient copilotCli, string reviewPrompt, string copilotToken, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Perform copilot analyzer report: {Environment.NewLine} {reviewPrompt}");
        await copilotCli.RunReviewAsync(reviewPrompt, copilotToken, cancellationToken);
    }
}
