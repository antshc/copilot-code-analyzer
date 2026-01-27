using ReviewApp.Infrastructure;

namespace ReviewApp.Core;

public class ChangeDetector
{
    private readonly IGitClient gitClient;

    public ChangeDetector(IGitClient gitClient) => this.gitClient = gitClient;

    // Returns changed C# files relative to HEAD, excluding Program.cs to avoid self-mutation loops.
    public async Task<IReadOnlyList<string>> GetChangedCSharpFilesAsync(CancellationToken cancellationToken = default)
    {
        var raw = await gitClient.DiffNameOnlyFromHeadAsync(cancellationToken).ConfigureAwait(false);
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
