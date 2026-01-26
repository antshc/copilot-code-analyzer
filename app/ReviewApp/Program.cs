using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class ReviewWorkflow
{
    private static readonly string RepoRoot = GetRepoRoot();
    private static readonly string ReportOut = Path.Combine(RepoRoot, "report");
    private static readonly string OutputDir = Path.Combine(RepoRoot, "_changes");
    private static readonly string ReviewPromptUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md";
    private static readonly string MinimalEditorConfigUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/rules/minimal.editorconfig";
    private static readonly string EditorConfigPath = Path.Combine(RepoRoot, ".editorconfig");
    private static readonly string EditorConfigBackupPath = Path.Combine(RepoRoot, ".editorconfig.backup");
    private static bool EditorConfigTempApplied = false;

    public static async Task Main(string[] args)
    {
        var appConfig = await ReadAppArgs(args);

        Console.WriteLine("Starting automated review workflow");

        // PrepareBranchState(appConfig.BaseBranchName, appConfig.BranchName);
        RecreateDirectory(ReportOut);

        if (appConfig.AnalyzersEnabled)
        {
            await RunAnalyzerBuildForChanges();
        }
        else
        {
            Console.WriteLine("Format prompt disabled; skipping analyzer and summary steps");
        }

        AuthenticateGitHub(appConfig.CopilotToken);
        RecreateDirectory(OutputDir);
        await CollectChanges();

        Console.WriteLine($"Downloading prompt from {ReviewPromptUrl}");
        string reviewPrompt = DownloadContent(ReviewPromptUrl);
        RunReviewPrompt(reviewPrompt);
        CleanupChangeArtifacts();
        RestoreBranchState(appConfig.BranchName);

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

    private static string GetRepoRoot() => RunGitCommand("rev-parse --show-toplevel");

    private static void PrepareBranchState(string baseBranchName, string branchName)
    {
        Console.WriteLine($"Preparing branch state using base '{baseBranchName}' against '{branchName}'");
        RunGitCommand("fetch");
        RunGitCommand($"checkout origin/{branchName}");
        RunGitCommand($"reset --soft $(git merge-base HEAD origin/{baseBranchName})");
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

        IReadOnlyList<string> changedFiles = GetChangedFiles();

        await ApplyMinimalEditorConfig();

        foreach (var projectPath in changedFiles.Select(FindProjectForFile).Distinct())
        {
            Console.WriteLine($"Running analyzer-enabled build for {projectPath}");

            RunDotnetCommand(
                $"build {projectPath} -p:EnableNETAnalyzers=true -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true -p:AnalysisLevel=latest -p:TreatWarningsAsErrors=false -p:GenerateDocumentationFile=true");
        }

        RestoreEditorConfigState();
    }

    private static IReadOnlyList<string> GetChangedFiles()
    {
        string files = RunGitCommand("diff --name-only HEAD");
        string[] filePaths = files.Split('\n');

        string[] changedFiles = filePaths
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Where(f => f.EndsWith(".cs")).ToArray();

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
        var content = DownloadContent(MinimalEditorConfigUrl);

        await File.WriteAllTextAsync(EditorConfigPath, content);
        EditorConfigTempApplied = true;
    }

    private static string DownloadContent(string url)
    {
        var content = RunCurlCommand($"-fsSL \"{url}\"");
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
        var changedFiles = GetChangedFiles();

        foreach (var file in changedFiles)
        {
            string targetPath = Path.Combine(OutputDir, file);
            string targetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(targetDir);

            await File.WriteAllTextAsync(targetPath, $"FILE: {file}\n\n----- ORIGINAL (HEAD) -----\n" +
                                                     RunGitCommand($"show HEAD:{file}") +
                                                     "\n----- DIFF -----\n" +
                                                     RunGitCommand($"diff HEAD -- {file}"));
        }
    }

    private static void RunReviewPrompt(string reviewPrompt)
    {
        Console.WriteLine("Running Copilot review prompt on collected diffs");
        RunCopilotCommand($"-p \"{reviewPrompt} @{OutputDir}. save results to {ReportOut}/review-report.md\" --yolo --model gpt-5.2");
    }

    private static void CleanupChangeArtifacts()
    {
        Console.WriteLine("Cleaning up change artifacts");

        if (Directory.Exists(OutputDir))
        {
            Directory.Delete(OutputDir, true);
        }
    }

    private static void RestoreBranchState(string branchName)
    {
        Console.WriteLine($"Restoring branch state for {branchName}");
        RunGitCommand($"checkout -B {branchName} origin/{branchName}");
    }

    private static string RunGitCommand(string arguments) => RunProcessCommand("git", arguments);

    private static string RunDotnetCommand(string command) => RunProcessCommand("dotnet", command);

    private static string RunCopilotCommand(string command) => RunProcessCommand("copilot", command);

    private static string RunCurlCommand(string command) => RunProcessCommand("curl", command);

    private static void AuthenticateGitHub(string ghToken)
    {
        Console.WriteLine("Authenticating GitHub CLI session");

        if (string.IsNullOrWhiteSpace(ghToken))
        {
            throw new ArgumentException("GitHub token must be provided.", nameof(ghToken));
        }

        Console.WriteLine("Authenticating GitHub CLI session");

        RunProcessCommand("gh", "auth login --with-token", ghToken);
    }

    private static string RunProcessCommand(string fileName, string arguments, string input = "")
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

        if (!string.IsNullOrWhiteSpace(input))
        {
            process.StandardInput.WriteLine(input);
            process.StandardInput.Close();
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output.Trim();
    }
}

public record AppConfig(
    string CopilotToken,
    string BaseBranchName,
    string BranchName,
    bool AnalyzersEnabled);
