namespace ReviewApp.Core.Abstractions;

public interface IFileSystemService
{
    // Deletes and recreates a directory to ensure a clean workspace.
    void RecreateDirectory(string targetDirectory);

    // Writes text content to a file, creating parent directories when needed.
    Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default);

    // Deletes a directory if it exists.
    void DeleteDirectoryIfExists(string targetDirectory);

    // Copies a file to a destination, overwriting if present.
    void CopyFile(string sourcePath, string destinationPath);

    // Moves a file to a destination, overwriting if present.
    void MoveFile(string sourcePath, string destinationPath);

    // Deletes a file if it exists.
    void DeleteFile(string path);
}
