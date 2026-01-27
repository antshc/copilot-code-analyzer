using ReviewApp.Core.Abstractions;

namespace ReviewApp.Core;

public class ChangeDetector : IChangeDetector
{
    private readonly HashSet<string> ExcludeFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Program.cs", "Reviewer.cs" };
    private readonly IGitClient _gitClient;

    public ChangeDetector(IGitClient gitClient) => _gitClient = gitClient;

    public async Task<IReadOnlyList<string>> GetChangedCSharpFilesAsync(CancellationToken cancellationToken)
    {
        var raw = await _gitClient.DiffNameOnlyFromHeadAsync(cancellationToken).ConfigureAwait(false);
        var filePaths = raw.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var changed = filePaths
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !ExcludeFileNames.Contains(Path.GetFileName(f)))
            .ToArray();

        if (!changed.Any())
        {
            Console.WriteLine("No changed C# files detected; skipping analyzer run");
        }

        return changed;
    }
}

public interface IChangeDetector
{
    Task<IReadOnlyList<string>> GetChangedCSharpFilesAsync(CancellationToken cancellationToken);
}
