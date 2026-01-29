namespace ReviewApp.Core.Analyzers;

public class ProjectLocator : IProjectLocator
{
    private readonly string _repoRoot;

    public ProjectLocator(string repoRoot)
        => _repoRoot = repoRoot;

    // Walks up from the file location to find the nearest .csproj to associate changes.
    public string FindProjectForFile(string relativeSourceFile)
    {
        var absolutePath = Path.Combine(_repoRoot, relativeSourceFile);
        var currentDir = Path.GetDirectoryName(absolutePath);

        while (!string.IsNullOrWhiteSpace(currentDir))
        {
            var candidates = Directory.GetFiles(currentDir, "*.csproj");

            if (candidates.Any())
            {
                return candidates.First();
            }

            currentDir = Path.GetDirectoryName(currentDir);
        }

        throw new FileNotFoundException($"No .csproj file found for {relativeSourceFile}, in the absolutePath {absolutePath}");
    }
}
