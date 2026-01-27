namespace ReviewApp.Core.Abstractions;

public interface IDiffCollector
{
    Task CollectAsync(IReadOnlyList<string> changedFiles, CancellationToken cancellationToken = default);
}
