using ReviewApp.Infrastructure;

namespace ReviewApp.Test;

class Program
{
    static async Task Main(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var processRunner = new ProcessRunner(currentDirectory);
        var downloader = new CurlDownloader(processRunner);
        var prompt = await downloader.DownloadStringAsync("https://raw.githubusercontent.com/antshc/copilot-code-analyzer/refs/heads/main/prompts/review.prompt.md", CancellationToken.None);
        var editorConfig = await downloader.DownloadStringAsync("https://raw.githubusercontent.com/antshc/copilot-code-analyzer/refs/heads/main/rules/minimal.editorconfig", CancellationToken.None);
        var appConfig = await GetAppConfig(currentDirectory, CancellationToken.None);

        var newArgs = new List<string>()
        {
            "--token",
            appConfig.CopilotToken,
            "--base-branch",
            appConfig.BaseBranchName,
            "--branch",
            appConfig.BranchName,
            "--review-prompt",
            prompt,
            "--editorconfig",
            editorConfig,
            "--analyzers",
            "enable"
        };

        await ReviewApp.Program.Main(newArgs.ToArray());
    }

    private static async Task<AppConfig> GetAppConfig(string currentDirectory, CancellationToken cancellationToken)
    {
        var appConfigJsonPath = Path.Combine(currentDirectory, "appsettings.local.json");

        AppConfig appConfig = await ConfigReader.Read(appConfigJsonPath, cancellationToken);

        return appConfig;
    }
}
//
// // Validate required arguments
// if (!parsedArgs.TryGetValue("--token", out var ghToken) || string.IsNullOrWhiteSpace(ghToken))
// {
//     throw new ArgumentException("Missing required argument: --token <GH_TOKEN>");
// }
//
// if (!parsedArgs.TryGetValue("--base-branch", out var baseBranchName) || string.IsNullOrWhiteSpace(baseBranchName))
// {
//     throw new ArgumentException("Missing required argument: --base-branch <BASE_BRANCH_NAME>");
// }
//
// if (!parsedArgs.TryGetValue("--branch", out var branchName) || string.IsNullOrWhiteSpace(branchName))
// {
//     throw new ArgumentException("Missing required argument: --branch <BRANCH_NAME>");
// }
//
// if (!parsedArgs.TryGetValue("--review-prompt", out var reviewPrompt) || string.IsNullOrWhiteSpace(reviewPrompt))
// {
//     throw new ArgumentException("Missing required argument: --review-prompt <REVIEW_PROMPT>");
// }
//
// if (!parsedArgs.TryGetValue("--editorconfig", out var editorconfig) || string.IsNullOrWhiteSpace(editorconfig))
// {
//     throw new ArgumentException("Missing required argument: --editorconfig <EDITORCONFIG>");
// }
//
// // Parse optional arguments with defaults
// var analyzersValue = parsedArgs.GetValueOrDefault("--analyzers", "enable");
// var analyzersEnabled = analyzersValue.Equals("enable", StringComparison.OrdinalIgnoreCase);
