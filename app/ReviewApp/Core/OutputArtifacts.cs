namespace ReviewApp.Core;

public record OutputArtifacts
{
    public OutputArtifacts(string repoRootDirectory)
    {
        RepoRootDirectory = repoRootDirectory;
        ReportOut = Path.Combine(RepoRootDirectory, "report");
        OutputDir = Path.Combine(RepoRootDirectory, "_changes");
        EditorConfigPath = Path.Combine(RepoRootDirectory, ".editorconfig");
        EditorConfigBackupPath = Path.Combine(RepoRootDirectory, ".editorconfig.backup");
    }

    public string RepoRootDirectory { get; }
    public string ReportOut { get; }
    public string OutputDir { get; }
    public string EditorConfigPath { get; }
    public string EditorConfigBackupPath { get; }
}
