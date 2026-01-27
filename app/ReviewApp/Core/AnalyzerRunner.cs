using ReviewApp.Infrastructure;

namespace ReviewApp.Core;

public class AnalyzerRunner
{
    private readonly IDotnetCli dotnetCli;
    private readonly EditorConfigManager editorConfigManager;
    private readonly IFileSystemService fileSystemService;
    private readonly ProjectLocator projectLocator;
    private readonly string reportDirectory;

    public AnalyzerRunner(
        IDotnetCli dotnetCli,
        EditorConfigManager editorConfigManager,
        IFileSystemService fileSystemService,
        ProjectLocator projectLocator,
        string reportDirectory)
    {
        this.dotnetCli = dotnetCli;
        this.editorConfigManager = editorConfigManager;
        this.fileSystemService = fileSystemService;
        this.projectLocator = projectLocator;
        this.reportDirectory = reportDirectory;
    }

    // Runs analyzer-enabled builds for projects containing changed files and writes diagnostics.
    public async Task RunAsync(IReadOnlyList<string> changedFiles, CancellationToken cancellationToken = default)
    {
        if (!changedFiles.Any())
        {
            return;
        }

        var projectToFiles = MapProjectsToFiles(changedFiles);

        await editorConfigManager.ApplyMinimalConfigAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var projectPath in projectToFiles.Keys)
            {
                Console.WriteLine($"Running analyzer-enabled build for {projectPath}");
                var result = await dotnetCli.BuildWithAnalyzersAsync(projectPath, cancellationToken).ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException($"dotnet build failed for {projectPath}: {result.StandardError.Trim()}");
                }

                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                var buildLogFileName = Path.Combine(reportDirectory, $"{projectName}.log");
                await fileSystemService.WriteFileAsync(buildLogFileName, result.StandardOutput, cancellationToken).ConfigureAwait(false);

                var fileNames = projectToFiles[projectPath].Select(Path.GetFileNameWithoutExtension).ToArray();
                var filteredLines = result.StandardOutput
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => fileNames.Any(line.Contains))
                    .ToArray();

                var buildDiagFileName = Path.Combine(reportDirectory, $"{projectName}.diag.log");
                await fileSystemService.WriteFileAsync(buildDiagFileName, string.Join(Environment.NewLine, filteredLines), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            editorConfigManager.RestoreOriginal();
        }
    }

    private Dictionary<string, HashSet<string>> MapProjectsToFiles(IReadOnlyList<string> changedFiles)
    {
        // Groups changed files by their owning project to minimize build invocations.
        var projectToFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in changedFiles)
        {
            var projectPath = projectLocator.FindProjectForFile(filePath);

            if (!projectToFiles.TryGetValue(projectPath, out var files))
            {
                files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                projectToFiles[projectPath] = files;
            }

            files.Add(filePath);
        }

        return projectToFiles;
    }
}
