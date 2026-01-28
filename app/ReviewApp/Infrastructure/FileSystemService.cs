using ReviewApp.Core.Abstractions;

namespace ReviewApp.Infrastructure;

public class FileSystemService : IFileSystemService
{
    private readonly string _repoRootDirectory;

    public FileSystemService(string repoRootDirectory) => _repoRootDirectory = repoRootDirectory;

    // Ensures the directory exists and is empty for deterministic outputs.
    public void RecreateDirectory(string targetDirectory)
    {
        targetDirectory = Path.Combine(_repoRootDirectory, targetDirectory);

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }

        Directory.CreateDirectory(targetDirectory);
    }

    // Writes content to a file and creates parent directories as needed.
    public async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        path = Path.Combine(_repoRootDirectory, path);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public void DeleteFileIfExists(string path)
    {
        path = Path.Combine(_repoRootDirectory, path);
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }

    // Removes an existing directory recursively.
    public void DeleteDirectoryIfExists(string targetDirectory)
    {
        targetDirectory = Path.Combine(_repoRootDirectory, targetDirectory);
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }
    }

    // Creates or overwrites a file copy.
    public void CopyFile(string sourcePath, string destinationPath)
    {
        sourcePath = Path.Combine(_repoRootDirectory, sourcePath);
        destinationPath = Path.Combine(_repoRootDirectory, destinationPath);
        File.Copy(sourcePath, destinationPath, true);
    }

    // Moves a file, overwriting existing content if present.
    public void MoveFile(string sourcePath, string destinationPath)
    {
        sourcePath = Path.Combine(_repoRootDirectory, sourcePath);
        destinationPath = Path.Combine(_repoRootDirectory, destinationPath);
        File.Move(sourcePath, destinationPath, true);
    }

    // Deletes a file if it exists to leave the tree clean.
    public void DeleteFile(string path)
    {
        path = Path.Combine(_repoRootDirectory, path);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async Task<string> ReadFileAsync(string file, CancellationToken cancellationToken)
    {
        file = Path.Combine(_repoRootDirectory, file);
        return await File.ReadAllTextAsync(file, cancellationToken);
    }
}
