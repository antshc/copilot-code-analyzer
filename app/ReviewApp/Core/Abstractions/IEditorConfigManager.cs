namespace ReviewApp.Core.Abstractions;

public interface IEditorConfigManager
{
    Task ApplyMinimalConfigAsync(CancellationToken cancellationToken);
    void RestoreOriginal();
}
