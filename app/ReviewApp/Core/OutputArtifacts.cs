namespace ReviewApp.Core;

public record OutputArtifacts
{
    public OutputArtifacts(string repoRootDirectory)
    {
        RepoRootDirectory = repoRootDirectory;
        ReportOut = Path.Combine(RepoRootDirectory, "report");
        OutputDir = Path.Combine(RepoRootDirectory, "_changes");
        OriginalEditorConfigPath = Path.Combine(RepoRootDirectory, ".editorconfig");
        EditorConfigBackupPath = Path.Combine(RepoRootDirectory, ".editorconfig.backup");
    }

    public string RepoRootDirectory { get; }
    public string ReportOut { get; }
    public string OutputDir { get; }
    public string OriginalEditorConfigPath { get; }
    public string EditorConfigBackupPath { get; }
}
