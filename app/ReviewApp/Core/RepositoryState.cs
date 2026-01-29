using ReviewApp.Core.Abstractions;

namespace ReviewApp.Core;

internal class BranchState : IBranchState
{
    private readonly IGitClient _gitClient;

    public BranchState(IGitClient gitClient)
        => _gitClient = gitClient;

    public async Task SetReviewBranch(string baseBranchName, string branchName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Preparing branch state using base '{baseBranchName}' against '{branchName}'");
        await _gitClient.FetchAsync(cancellationToken);
        await _gitClient.CheckoutAsync($"origin/{branchName}", cancellationToken);
        var commit = await _gitClient.FindMergeBaseAsync("HEAD", $"origin/{baseBranchName}", cancellationToken);
        await _gitClient.ResetSoftAsync(commit, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}

internal interface IBranchState
{
    Task SetReviewBranch(string baseBranchName, string branchName, CancellationToken cancellationToken);
}
