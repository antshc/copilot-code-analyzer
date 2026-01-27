using ReviewApp.Core.Abstractions;
using ReviewApp.Infrastructure;

namespace ReviewApp.Core;

public class ChangeDetector : IChangeDetector
{
    private readonly IGitClient _gitClient;

    public ChangeDetector(IGitClient gitClient) => _gitClient = gitClient;

    public async Task<IReadOnlyList<string>> GetChangedCSharpFilesAsync(CancellationToken cancellationToken)
    {
        var raw = await _gitClient.DiffNameOnlyFromHeadAsync(cancellationToken).ConfigureAwait(false);
        var filePaths = raw.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var changed = filePaths
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !string.Equals(Path.GetFileName(f), "Program.cs", StringComparison.OrdinalIgnoreCase))
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
