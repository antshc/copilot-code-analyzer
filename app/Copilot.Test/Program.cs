using ReviewApp;
using ReviewApp.Infrastructure;

namespace Copilot.Test;

class Program
{
    private const string LocalConfigFileName = "appsettings.local.json";
    private const string ReportRoot = "Report";
    private const string ReportOut = $"{ReportRoot}/copilot_review.txt";

    static async Task Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var currentDirectory = Directory.GetCurrentDirectory();

        SetupOutputDirectories(currentDirectory);

        var copilot = CreateCopilotClient(currentDirectory);
        var appConfig = await GetAppConfig(currentDirectory, cancellationToken);

        // 
        await copilot.RunReviewAsync($"What files do you see in the @Changes. save results to {ReportOut}", appConfig.CopilotToken, cancellationToken);
        
        // copilot -p "What files do you see in the @." --model gpt-5.2 --allow-all-tools

        Console.WriteLine("Review completed");
    }

    private static void SetupOutputDirectories(string currentDirectory) => Directory.CreateDirectory(Path.Combine(currentDirectory, ReportRoot));

    private static async Task<AppConfig> GetAppConfig(string currentDirectory, CancellationToken cancellationToken)
    {
        var appConfigJsonPath = Path.Combine(currentDirectory, LocalConfigFileName);

        AppConfig appConfig = await ConfigReader.Read(appConfigJsonPath, cancellationToken);

        return appConfig;
    }

    private static CopilotClient CreateCopilotClient(string currentDirectory)
    {
        var processRunner = new ProcessRunner(currentDirectory);
        var copilot = new CopilotClient(processRunner);

        return copilot;
    }
}
