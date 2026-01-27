using ReviewApp.Core.Abstractions;

namespace ReviewApp.Infrastructure;

public class GitClient : IGitClient
{
    private readonly IProcessRunner processRunner;

    public GitClient(IProcessRunner processRunner) => this.processRunner = processRunner;

    // Runs git to discover the repository root.
    public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default) =>
        RunGitCaptureAsync("rev-parse --show-toplevel", cancellationToken);

    // Runs git fetch to refresh refs.
    public Task FetchAsync(CancellationToken cancellationToken = default) =>
        RunGitEnsureSuccessAsync("fetch", cancellationToken);

    // Checks out a ref such as origin/branch or a commit SHA.
    public Task CheckoutAsync(string reference, CancellationToken cancellationToken = default) =>
        RunGitEnsureSuccessAsync($"checkout {reference}", cancellationToken);

    // Finds merge-base between two refs.
    public Task<string> FindMergeBaseAsync(string leftRef, string rightRef, CancellationToken cancellationToken = default) =>
        RunGitCaptureAsync($"merge-base {leftRef} {rightRef}", cancellationToken);

    // Soft resets to a commit while keeping index changes.
    public Task ResetSoftAsync(string commit, CancellationToken cancellationToken = default) =>
        RunGitEnsureSuccessAsync($"reset --soft {commit}", cancellationToken);

    // Gets changed file paths relative to HEAD.
    public Task<string> DiffNameOnlyFromHeadAsync(CancellationToken cancellationToken = default) =>
        RunGitCaptureAsync("diff --name-only HEAD", cancellationToken);

    // Gets file content at HEAD.
    public Task<string> ShowFileFromHeadAsync(string relativePath, CancellationToken cancellationToken = default) =>
        RunGitCaptureAsync($"show HEAD:{relativePath}", cancellationToken);

    // Gets per-file diff relative to HEAD.
    public Task<string> DiffFileFromHeadAsync(string relativePath, CancellationToken cancellationToken = default) =>
        RunGitCaptureAsync($"diff HEAD -- {relativePath}", cancellationToken);

    // Resets local branch to match remote state.
    public Task CheckoutBranchResetAsync(string branchName, CancellationToken cancellationToken = default) =>
        RunGitEnsureSuccessAsync($"checkout -B {branchName} origin/{branchName}", cancellationToken);

    private async Task<string> RunGitCaptureAsync(string arguments, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync("git", arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureSuccess(arguments, result);

        return result.StandardOutput.Trim();
    }

    private async Task RunGitEnsureSuccessAsync(string arguments, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync("git", arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureSuccess(arguments, result);
    }

    private static void EnsureSuccess(string arguments, CommandResult result)
    {
        // Throws with stderr when git fails so callers can react appropriately.
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git {arguments} failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }
}
