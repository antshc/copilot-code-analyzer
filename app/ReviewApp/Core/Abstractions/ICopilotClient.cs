namespace ReviewApp.Core.Abstractions;

public interface ICopilotClient
{
    // Executes the Copilot CLI with the provided prompt and token.
    Task RunReviewAsync(string promptContent, string token, CancellationToken cancellationToken = default);
}
