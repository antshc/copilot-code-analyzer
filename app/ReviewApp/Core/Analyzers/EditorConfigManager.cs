using ReviewApp.Core.Abstractions;

namespace ReviewApp.Core.Analyzers;

public class EditorConfigManager : IEditorConfigManager
{
    private readonly IFileSystemService _fileSystemService;
    private readonly string _editorConfig;
    private readonly string _originalEditorConfigPath;
    private readonly string _backupPath;
    private bool isApplied;

    public EditorConfigManager(
        IFileSystemService fileSystemService,
        string editorConfig,
        string originalEditorConfigPath,
        string backupPath)
    {
        _fileSystemService = fileSystemService;
        _editorConfig = editorConfig;
        _originalEditorConfigPath = originalEditorConfigPath;
        _backupPath = backupPath;
    }

    // Applies the minimal .editorconfig, backing up any existing configuration.
    public async Task ApplyMinimalConfigAsync(CancellationToken cancellationToken)
    {
        if (isApplied)
        {
            return;
        }

        if (File.Exists(_originalEditorConfigPath))
        {
            _fileSystemService.CopyFile(_originalEditorConfigPath, _backupPath);
        }

        await _fileSystemService.WriteFileAsync(_originalEditorConfigPath, _editorConfig, cancellationToken);
        isApplied = true;
    }

    // Restores the original .editorconfig or removes the temporary one.
    public void RestoreOriginal()
    {
        if (!isApplied)
        {
            return;
        }

        if (File.Exists(_backupPath))
        {
            _fileSystemService.MoveFile(_backupPath, _originalEditorConfigPath);
        }
        else
        {
            _fileSystemService.DeleteFile(_originalEditorConfigPath);
        }

        isApplied = false;
    }
}
