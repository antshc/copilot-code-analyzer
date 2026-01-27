namespace ReviewApp.Core.Abstractions;

public interface IGitClient
{
    // Returns repository root path using git metadata.
    Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default);

    // Fetches remote refs to ensure branch operations have current data.
    Task FetchAsync(CancellationToken cancellationToken = default);

    // Checks out the provided reference (branch, commit, or tag).
    Task CheckoutAsync(string reference, CancellationToken cancellationToken = default);

    // Finds the merge-base between two references for diff baselines.
    Task<string> FindMergeBaseAsync(string leftRef, string rightRef, CancellationToken cancellationToken = default);

    // Performs a soft reset to the specified commit, keeping working tree changes staged.
    Task ResetSoftAsync(string commit, CancellationToken cancellationToken = default);

    // Lists files changed relative to HEAD.
    Task<string> DiffNameOnlyFromHeadAsync(CancellationToken cancellationToken = default);

    // Shows file content from HEAD for a given relative path.
    Task<string> ShowFileFromHeadAsync(string relativePath, CancellationToken cancellationToken = default);

    // Produces a diff for a single file relative to HEAD.
    Task<string> DiffFileFromHeadAsync(string relativePath, CancellationToken cancellationToken = default);

    // Resets a branch to match its remote tracking reference.
    Task CheckoutBranchResetAsync(string branchName, CancellationToken cancellationToken = default);
}
