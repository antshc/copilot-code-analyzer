namespace ReviewApp.Infrastructure;

public class EditorConfigManager : IEditorConfigManager
{
    private static readonly string MinimalEditorConfigUrl = "https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/rules/minimal.editorconfig";

    private readonly IFileSystemService fileSystemService;
    private readonly IContentDownloader downloader;
    private readonly string editorConfigPath;
    private readonly string backupPath;
    private bool isApplied;

    public EditorConfigManager(
        IFileSystemService fileSystemService,
        IContentDownloader downloader,
        string editorConfigPath,
        string backupPath)
    {
        this.fileSystemService = fileSystemService;
        this.downloader = downloader;
        this.editorConfigPath = editorConfigPath;
        this.backupPath = backupPath;
    }

    // Applies the minimal .editorconfig, backing up any existing configuration.
    public async Task ApplyMinimalConfigAsync(CancellationToken cancellationToken = default)
    {
        if (isApplied)
        {
            return;
        }

        if (File.Exists(editorConfigPath))
        {
            fileSystemService.CopyFile(editorConfigPath, backupPath);
        }

        var content = await downloader.DownloadStringAsync(MinimalEditorConfigUrl, cancellationToken).ConfigureAwait(false);
        await fileSystemService.WriteFileAsync(editorConfigPath, content, cancellationToken).ConfigureAwait(false);
        isApplied = true;
    }

    // Restores the original .editorconfig or removes the temporary one.
    public void RestoreOriginal()
    {
        if (!isApplied)
        {
            return;
        }

        if (File.Exists(backupPath))
        {
            fileSystemService.MoveFile(backupPath, editorConfigPath);
        }
        else
        {
            fileSystemService.DeleteFile(editorConfigPath);
        }

        isApplied = false;
    }
}

public interface IEditorConfigManager
{
    Task ApplyMinimalConfigAsync(CancellationToken cancellationToken = default);
    void RestoreOriginal();
}
