using ReviewApp.Core.Abstractions;

namespace ReviewApp.Infrastructure;

public class CopilotClient : ICopilotClient
{
    private readonly IProcessRunner _processRunner;

    public CopilotClient(IProcessRunner processRunner) => _processRunner = processRunner;

    // Runs Copilot CLI against collected diffs and saves the review output.
    public async Task RunReviewAsync(string promptContent, string token, CancellationToken cancellationToken)
    {
        var promptArgument = $"-p \"{promptContent}\" --model gpt-5.2 --allow-all-tools --deny-tool \"github-mcp-server\" --deny-tool \"dotnet\"";
        var environment = new Dictionary<string, string?> { { "GH_TOKEN", token } };
        var result = await _processRunner.RunAsync("copilot", promptArgument, environment, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Copilot CLI failed: {result.StandardError.Trim()}");
        }
    }
}
