namespace ReviewApp.Infrastructure;

public class EditorConfigManager
{
    private readonly IFileSystemService fileSystemService;
    private readonly IContentDownloader downloader;
    private readonly string editorConfigPath;
    private readonly string backupPath;
    private readonly string minimalConfigUrl;
    private bool isApplied;

    public EditorConfigManager(
        IFileSystemService fileSystemService,
        IContentDownloader downloader,
        string editorConfigPath,
        string backupPath,
        string minimalConfigUrl)
    {
        this.fileSystemService = fileSystemService;
        this.downloader = downloader;
        this.editorConfigPath = editorConfigPath;
        this.backupPath = backupPath;
        this.minimalConfigUrl = minimalConfigUrl;
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

        var content = await downloader.DownloadStringAsync(minimalConfigUrl, cancellationToken).ConfigureAwait(false);
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
