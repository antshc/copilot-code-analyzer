namespace ReviewApp.Infrastructure;

public class FileSystemService : IFileSystemService
{
    // Ensures the directory exists and is empty for deterministic outputs.
    public void RecreateDirectory(string targetDirectory)
    {
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }

        Directory.CreateDirectory(targetDirectory);
    }

    // Writes content to a file and creates parent directories as needed.
    public async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    // Removes an existing directory recursively.
    public void DeleteDirectoryIfExists(string targetDirectory)
    {
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }
    }

    // Creates or overwrites a file copy.
    public void CopyFile(string sourcePath, string destinationPath)
        => File.Copy(sourcePath, destinationPath, true);

    // Moves a file, overwriting existing content if present.
    public void MoveFile(string sourcePath, string destinationPath)
        => File.Move(sourcePath, destinationPath, true);

    // Deletes a file if it exists to leave the tree clean.
    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

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
