namespace ReviewApp.Infrastructure;

public class CopilotClient : ICopilotClient
{
    private readonly IProcessRunner _processRunner;

    public CopilotClient(IProcessRunner processRunner) => _processRunner = processRunner;

    // Runs Copilot CLI against collected diffs and saves the review output.
    public async Task RunReviewAsync(string promptContent, string changesPath, string reportOutputPath, string token, CancellationToken cancellationToken = default)
    {
        var promptArgument = $"-p \"{promptContent} @{changesPath}. save results to {reportOutputPath}\" --yolo --model gpt-5.2";
        var environment = new Dictionary<string, string?> { { "GH_TOKEN", token } };
        var result = await _processRunner.RunAsync("copilot", promptArgument, environment, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Copilot CLI failed: {result.StandardError.Trim()}");
        }
    }
}

public interface ICopilotClient
{
    // Executes the Copilot CLI with the provided prompt and token.
    Task RunReviewAsync(string promptContent, string changesPath, string reportOutputPath, string token, CancellationToken cancellationToken = default);
}
