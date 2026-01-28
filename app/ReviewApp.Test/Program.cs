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
