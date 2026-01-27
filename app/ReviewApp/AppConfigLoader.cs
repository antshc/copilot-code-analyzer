using System.Text.Json;
using ReviewApp.Infrastructure;

namespace ReviewApp;

public static class AppConfigLoader
{
    private const string LocalConfigFileName = "appsettings.local.json";

    // Loads configuration from appsettings.local.json when present, otherwise from command-line arguments.
    public static async Task<AppConfig> LoadAsync(string[] args, CancellationToken cancellationToken)
    {
        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), LocalConfigFileName);

        if (File.Exists(jsonPath))
        {
            return await ConfigReader.Read(jsonPath, cancellationToken);
        }

        if (args.Length < 4)
        {
            throw new ArgumentException("Usage: ReviewWorkflow <GH_TOKEN> <BASE_BRANCH_NAME> <BRANCH_NAME> <SOLUTION_PATH> [-format enable|disable]");
        }

        var ghToken = args[0];
        var baseBranchName = args[1];
        var branchName = args[2];
        var formatPromptToggle = args.Length > 5 && args[4] == "-format" ? args[5] : "disable";
        var analyzersEnabled = formatPromptToggle == "enable";

        return new AppConfig(ghToken, baseBranchName, branchName, analyzersEnabled);
    }
}
