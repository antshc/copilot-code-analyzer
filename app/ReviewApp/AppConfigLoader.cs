using ReviewApp.Infrastructure;

namespace ReviewApp;

public static class AppConfigLoader
{
    private const string LocalConfigFileName = "appsettings.local.json";

    // Loads configuration from appsettings.local.json when present, otherwise from command-line arguments.
    public static async Task<AppConfig> LoadAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args is not null && args.Length > 0)
        {
            // Parse command-line arguments in --arg value format
            var parsedArgs = ParseCommandLineArguments(args);

            // Validate required arguments
            if (!parsedArgs.TryGetValue("--token", out var ghToken) || string.IsNullOrWhiteSpace(ghToken))
            {
                throw new ArgumentException("Missing required argument: --token <GH_TOKEN>");
            }

            if (!parsedArgs.TryGetValue("--base-branch", out var baseBranchName) || string.IsNullOrWhiteSpace(baseBranchName))
            {
                throw new ArgumentException("Missing required argument: --base-branch <BASE_BRANCH_NAME>");
            }

            if (!parsedArgs.TryGetValue("--branch", out var branchName) || string.IsNullOrWhiteSpace(branchName))
            {
                throw new ArgumentException("Missing required argument: --branch <BRANCH_NAME>");
            }

            if (!parsedArgs.TryGetValue("--review-prompt", out var reviewPrompt) || string.IsNullOrWhiteSpace(reviewPrompt))
            {
                throw new ArgumentException("Missing required argument: --review-prompt <REVIEW_PROMPT>");
            }
            
            if (!parsedArgs.TryGetValue("--code-analysis-prompt", out var codeAnalysisPrompt) || string.IsNullOrWhiteSpace(codeAnalysisPrompt))
            {
                throw new ArgumentException("Missing required argument: --code-analysis-prompt <CODE_ANALYSIS_PROMPT>");
            }

            if (!parsedArgs.TryGetValue("--editorconfig", out var editorconfig) || string.IsNullOrWhiteSpace(editorconfig))
            {
                throw new ArgumentException("Missing required argument: --editorconfig <EDITORCONFIG>");
            }

            // Parse optional arguments with defaults
            var analyzersValue = parsedArgs.GetValueOrDefault("--analyzers", "enable");
            var analyzersEnabled = analyzersValue.Equals("enable", StringComparison.OrdinalIgnoreCase);

            return new AppConfig(ghToken, baseBranchName, branchName, reviewPrompt, codeAnalysisPrompt, editorconfig, analyzersEnabled);
        }

        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), LocalConfigFileName);

        return await ConfigReader.Read(jsonPath, cancellationToken);
    }

    /// <summary>
    /// Parses command-line arguments in the format --arg value into a dictionary.
    /// </summary>
    /// <param name="args">Command-line arguments array</param>
    /// <returns>Dictionary mapping argument names to their values</returns>
    private static Dictionary<string, string> ParseCommandLineArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            // Check if current argument is a flag (starts with --)
            if (args[i].StartsWith("--") && i + 1 < args.Length)
            {
                var key = args[i];
                var value = args[i + 1];

                // Store the key-value pair
                result[key] = value;

                // Skip the next argument since it's the value
                i++;
            }
        }

        return result;
    }
}
