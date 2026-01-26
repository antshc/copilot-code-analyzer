using System.Diagnostics;

class ReviewWorkflow
{
    private static readonly string RepoRoot = GetRepoRoot();
    private static readonly string ReportOut = Path.Combine(RepoRoot, "report");
    private static readonly string OutputDir = Path.Combine(RepoRoot, "_changes");
    private static readonly string ReviewPromptUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md";
    private static readonly string MinimalEditorConfigUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/rules/minimal.editorconfig";
    private static readonly string EditorConfigPath = Path.Combine(RepoRoot, ".editorconfig");
    private static readonly string EditorConfigBackupPath = Path.Combine(RepoRoot, ".editorconfig.backup");
    private static bool EditorConfigTempApplied = false;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting automated review workflow");

        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: ReviewWorkflow <GH_TOKEN> <BASE_BRANCH_NAME> <BRANCH_NAME> <SOLUTION_PATH> [-format enable|disable]");
            Environment.Exit(1);
        }

        string ghToken = args[0];
        string baseBranchName = args[1];
        string branchName = args[2];
        string solutionPath = args[3];
        string formatPromptToggle = args.Length > 5 && args[4] == "-format" ? args[5] : "disable";

        PrepareBranchState(baseBranchName, branchName);
        RecreateDirectory(ReportOut);

        if (formatPromptToggle == "enable")
        {
            await RunAnalyzerBuildForChanges(solutionPath);
        }
        else
        {
            Console.WriteLine("Format prompt disabled; skipping analyzer and summary steps");
        }

        AuthenticateGitHub(ghToken);
        RecreateDirectory(OutputDir);
        CollectFileDiffs();

        string reviewPrompt = await DownloadPrompt(ReviewPromptUrl);
        RunReviewPrompt(reviewPrompt);
        CleanupChangeArtifacts();
        RestoreBranchState(branchName);

        Console.WriteLine("Review workflow completed");
    }

    private static string GetRepoRoot()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --show-toplevel",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return output;
    }

    private static void PrepareBranchState(string baseBranchName, string branchName)
    {
        Console.WriteLine($"Preparing branch state using base '{baseBranchName}' against '{branchName}'");
        RunCommand("git fetch");
        RunCommand($"git checkout origin/{branchName}");
        RunCommand($"git reset --soft $(git merge-base HEAD origin/{baseBranchName})");
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

    private static async Task<string> DownloadPrompt(string url)
    {
        Console.WriteLine($"Downloading prompt from {url}");
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    private static async Task RunAnalyzerBuildForChanges(string solutionPath)
    {
        Console.WriteLine("Running analyzer-enabled build for changes");
        var changedFiles = RunCommand("git diff --name-only HEAD -- '*.cs'").Split('\n').Where(f => !string.IsNullOrWhiteSpace(f)).ToArray();

        if (!changedFiles.Any())
        {
            Console.WriteLine("No changed C# files detected; skipping analyzer run");
            return;
        }

        ApplyMinimalEditorConfig();
        await Task.Delay(2000);

        foreach (var projectPath in changedFiles.Select(FindProjectForFile).Distinct())
        {
            Console.WriteLine($"Running analyzer-enabled build for {projectPath}");
            RunCommand($"dotnet build {projectPath} -p:EnableNETAnalyzers=true -p:AnalysisMode=Recommended -p:EnforceCodeStyleInBuild=true -p:AnalysisLevel=latest -p:TreatWarningsAsErrors=false -p:GenerateDocumentationFile=true");
        }

        RestoreEditorConfigState();
    }

    private static string FindProjectForFile(string sourceFile)
    {
        string currentDir = Path.GetDirectoryName(sourceFile);
        while (!string.IsNullOrEmpty(currentDir))
        {
            var candidates = Directory.GetFiles(currentDir, "*.csproj");
            if (candidates.Any())
            {
                return candidates.First();
            }
            currentDir = Path.GetDirectoryName(currentDir);
        }
        throw new FileNotFoundException($"No .csproj file found for {sourceFile}");
    }

    private static void ApplyMinimalEditorConfig()
    {
        if (EditorConfigTempApplied) return;

        if (File.Exists(EditorConfigPath))
        {
            Console.WriteLine("Backing up existing .editorconfig before running dotnet format");
            File.Copy(EditorConfigPath, EditorConfigBackupPath, true);
        }

        Console.WriteLine("Downloading minimal .editorconfig used solely for analyzer execution");
        using var client = new HttpClient();
        File.WriteAllText(EditorConfigPath, client.GetStringAsync(MinimalEditorConfigUrl).Result);

        EditorConfigTempApplied = true;
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

    private static void AuthenticateGitHub(string ghToken)
    {
        Console.WriteLine("Authenticating GitHub CLI session");
        RunCommand($"echo {ghToken} | gh auth login --with-token");
    }

    private static void CollectFileDiffs()
    {
        Console.WriteLine("Collecting file diffs for changed C# files");
        var files = RunCommand("git diff --name-only HEAD -- '*.cs'").Split('\n').Where(f => !string.IsNullOrWhiteSpace(f));

        foreach (var file in files)
        {
            string targetPath = Path.Combine(OutputDir, file);
            string targetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(targetDir);

            File.WriteAllText(targetPath, $"FILE: {file}\n\n----- ORIGINAL (HEAD) -----\n" +
                RunCommand($"git show HEAD:{file}") +
                "\n----- DIFF -----\n" +
                RunCommand($"git diff HEAD -- {file}"));
        }
    }

    private static void RunReviewPrompt(string reviewPrompt)
    {
        Console.WriteLine("Running Copilot review prompt on collected diffs");
        RunCommand($"copilot -p \"{reviewPrompt} @{OutputDir}. save results to {ReportOut}/review-report.md\" --yolo --model gpt-5.2");
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
        RunCommand($"git checkout -B {branchName} origin/{branchName}");
    }

    private static string RunCommand(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
