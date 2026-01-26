using System.Diagnostics;
using System.Text;
using System.Text.Json;

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
        RepoRoot = await GetRepoRoot();
        ReportOut = Path.Combine(RepoRoot, "report");
        OutputDir = Path.Combine(RepoRoot, "_changes");
        EditorConfigPath = Path.Combine(RepoRoot, ".editorconfig");
        EditorConfigBackupPath = Path.Combine(RepoRoot, ".editorconfig.backup");
        var appConfig = await ReadAppArgs(args);

        Console.WriteLine("Starting automated review workflow");

        await PrepareBranchState(appConfig.BaseBranchName, appConfig.BranchName);
        RecreateDirectory(ReportOut);

        if (appConfig.AnalyzersEnabled)
        {
            await RunAnalyzerBuildForChanges();
        }
        else
        {
            Console.WriteLine("Format prompt disabled; skipping analyzer and summary steps");
        }

        // await AuthenticateGitHub(appConfig.CopilotToken);
        RecreateDirectory(OutputDir);
        await CollectChanges();

        Console.WriteLine($"Downloading prompt from {ReviewPromptUrl}");
        string reviewPrompt = await DownloadContent(ReviewPromptUrl);
        await RunReviewPrompt(reviewPrompt, appConfig.CopilotToken);
        CleanupChangeArtifacts();
        await RestoreBranchState(appConfig.BranchName);

        Console.WriteLine("Review workflow completed");
    }

    private static async Task<AppConfig> ReadAppArgs(string[] args)
    {
        string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json");

        if (File.Exists(jsonPath))
        {
            string json = await File.ReadAllTextAsync(jsonPath);

            var appConfig = JsonSerializer.Deserialize<AppConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return appConfig;
        }

        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: ReviewWorkflow <GH_TOKEN> <BASE_BRANCH_NAME> <BRANCH_NAME> <SOLUTION_PATH> [-format enable|disable]");
            Environment.Exit(1);
        }

        string ghToken = args[0];
        string baseBranchName = args[1];
        string branchName = args[2];
        string formatPromptToggle = args.Length > 5 && args[4] == "-format" ? args[5] : "disable";
        bool analyzersEnabled = formatPromptToggle == "enable";

        return new AppConfig(ghToken, baseBranchName, branchName, analyzersEnabled);
    }

    private static async Task<string> GetRepoRoot() => await RunGitCommand("rev-parse --show-toplevel");

    private static async Task PrepareBranchState(string baseBranchName, string branchName)
    {
        Console.WriteLine($"Preparing branch state using base '{baseBranchName}' against '{branchName}'");
        await RunGitCommand("fetch");
        await RunGitCommand($"checkout origin/{branchName}");
        var commit = await RunGitCommand($"merge-base HEAD origin/{baseBranchName}");
        await RunGitCommand($"reset --soft {commit}");
    }

    private static void RecreateDirectory(string targetDir)
    {
        Console.WriteLine($"Resetting directory at {targetDir}");

        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, true);
        }

        Directory.CreateDirectory(targetDir);
    }

    private static async Task RunAnalyzerBuildForChanges()
    {
        Console.WriteLine("Running analyzer-enabled build for changes");

        IReadOnlyList<string> changedFiles = await GetChangedFiles();

        await ApplyMinimalEditorConfig();

        // Maps each project to its set of changed files, handling cases where FindProjectForFile may throw.
        var projectToFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in changedFiles)
        {
            var projectPath = FindProjectForFile(filePath);

            if (!projectToFiles.TryGetValue(projectPath, out var files))
            {
                files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                projectToFiles[projectPath] = files;
            }

            files.Add(filePath);
        }

        foreach (var projectPath in projectToFiles.Keys)
        {
            Console.WriteLine($"Running analyzer-enabled build for {projectPath}");

            string buildOutput = await RunDotnetCommand(
                                     $"build {projectPath} -p:EnableNETAnalyzers=true -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true -p:AnalysisLevel=latest -p:TreatWarningsAsErrors=false -p:GenerateDocumentationFile=true");

            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var buildLogFileName = Path.Combine(ReportOut, $"{projectName}.log");
            await File.WriteAllTextAsync(buildLogFileName, buildOutput);

            var buildLogFileLines = await File.ReadAllLinesAsync(buildLogFileName);
            var fileNames = projectToFiles[projectPath].Select(Path.GetFileNameWithoutExtension);

            var filteredLines = buildLogFileLines
                .Where(line => fileNames.Any(line.Contains))
                .ToArray();

            var buildDiagFileName = Path.Combine(ReportOut, $"{projectName}.diag.log");
            await File.WriteAllTextAsync(buildDiagFileName, string.Join(Environment.NewLine, filteredLines));
        }

        RestoreEditorConfigState();
    }

    private static async Task<IReadOnlyList<string>> GetChangedFiles()
    {
        string files = await RunGitCommand("diff --name-only HEAD");
        string[] filePaths = files.Split(Environment.NewLine);

        string[] changedFiles = filePaths
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Where(f => f.EndsWith(".cs")).ToArray();

        changedFiles = changedFiles.Where(f => !f.Contains("Program.cs")).ToArray();

        if (!changedFiles.Any())
        {
            Console.WriteLine("No changed C# files detected; skipping analyzer run");

            return [];
        }

        return changedFiles;
    }

    private static string FindProjectForFile(string sourceFile)
    {
        string currentDir = Path.GetDirectoryName(sourceFile);

        while (!string.IsNullOrEmpty(currentDir))
        {
            var pathFromRoot = Path.Combine(RepoRoot, currentDir);
            var candidates = Directory.GetFiles(pathFromRoot, "*.csproj");

            if (candidates.Any())
            {
                return candidates.First();
            }

            currentDir = Path.GetDirectoryName(currentDir);
        }

        throw new FileNotFoundException($"No .csproj file found for {sourceFile}");
    }

    private static async Task ApplyMinimalEditorConfig()
    {
        if (EditorConfigTempApplied) return;

        if (File.Exists(EditorConfigPath))
        {
            Console.WriteLine("Backing up existing .editorconfig before running dotnet format");
            File.Copy(EditorConfigPath, EditorConfigBackupPath, true);
        }

        Console.WriteLine("Downloading minimal .editorconfig used solely for analyzer execution");
        var content = await DownloadContent(MinimalEditorConfigUrl);

        await File.WriteAllTextAsync(EditorConfigPath, content);
        EditorConfigTempApplied = true;
    }

    private static async Task<string> DownloadContent(string url)
    {
        var content = await RunCurlCommand($"-fsSL \"{url}\"");

        return content;
    }

    private static void RestoreEditorConfigState()
    {
        if (!EditorConfigTempApplied) return;

        if (File.Exists(EditorConfigBackupPath))
        {
            Console.WriteLine("Restoring original .editorconfig after dotnet format run");
            File.Move(EditorConfigBackupPath, EditorConfigPath, true);
        }
        else
        {
            Console.WriteLine("Removing temporary .editorconfig to leave the repo unchanged");
            File.Delete(EditorConfigPath);
        }

        EditorConfigTempApplied = false;
    }

    private static async Task CollectChanges()
    {
        Console.WriteLine("Collecting file diffs for changed C# files");
        IReadOnlyList<string> changedFiles = await GetChangedFiles();

        foreach (var file in changedFiles)
        {
            string targetPath = Path.Combine(OutputDir, file);
            string targetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(targetDir);

            await File.WriteAllTextAsync(targetPath, $"FILE: {file}\n\n----- ORIGINAL (HEAD) -----\n" +
                                                     await RunGitCommand($"show HEAD:{file}") +
                                                     "\n----- DIFF -----\n" +
                                                     await RunGitCommand($"diff HEAD -- {file}"));
        }
    }

    private static async Task RunReviewPrompt(string reviewPrompt, string token)
    {
        Console.WriteLine("Running Copilot review prompt on collected diffs");
        await RunCopilotCommand($"-p \"{reviewPrompt} @{OutputDir}. save results to {ReportOut}/review-report.md\" --yolo --model gpt-5.2", token);
    }

    private static void CleanupChangeArtifacts()
    {
        Console.WriteLine("Cleaning up change artifacts");

        if (Directory.Exists(OutputDir))
        {
            Directory.Delete(OutputDir, true);
        }
    }

    private static async Task RestoreBranchState(string branchName)
    {
        Console.WriteLine($"Restoring branch state for {branchName}");
        await RunGitCommand($"checkout -B {branchName} origin/{branchName}");
    }

    private static async Task<string> RunGitCommand(string arguments) => await RunProcessCommand("git", arguments);

    private static async Task<string> RunDotnetCommand(string command) => await RunProcessCommand("dotnet", command);

    private static async Task<string> RunCopilotCommand(string command, string token) => await RunProcessCommand("copilot", command, token);

    private static async Task<string> RunCurlCommand(string command) => await RunProcessCommand("curl", command);

    private static async Task<string> RunProcessCommand(string fileName, string arguments, string ghToken = "", CancellationToken cancellationToken = default)
    {
        // Executes an external process and returns its output; trim behavior is configurable per caller.
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.Start();

        if (!string.IsNullOrWhiteSpace(ghToken))
        {
            process.StartInfo.Environment["GH_TOKEN"] = ghToken;
        }

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();

        Task stdoutTask = StreamLinesAsync(
            process.StandardOutput,
            line =>
            {
                Console.WriteLine(line);
                stdoutBuffer.AppendLine(line);
            },
            cancellationToken);

        Task stderrTask = StreamLinesAsync(
            process.StandardError,
            line =>
            {
                Console.Error.WriteLine(line);
                stderrBuffer.AppendLine(line);
            },
            cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var errorText = stderrBuffer.ToString().Trim();

            throw new InvalidOperationException(
                $"Process '{fileName}' exited with code {process.ExitCode}. {errorText}".Trim());
        }

        var combinedOutput = stdoutBuffer.Length > 0
                                 ? stdoutBuffer.ToString()
                                 : stderrBuffer.ToString();

        return combinedOutput.Trim();
    }

    static async Task StreamLinesAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        // Reads lines until the stream ends; prints each line immediately.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                return;
            }

            onLine(line);
        }
    }
}

public record AppConfig(
    string CopilotToken,
    string BaseBranchName,
    string BranchName,
    bool AnalyzersEnabled);
