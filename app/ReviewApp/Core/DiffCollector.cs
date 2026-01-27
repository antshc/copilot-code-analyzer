using ReviewApp.Core.Abstractions;
using ReviewApp.Infrastructure;

namespace ReviewApp.Core;

public class DiffCollector : IDiffCollector
{
    private readonly IGitClient _gitClient;
    private readonly IFileSystemService _fileSystemService;
    private readonly string _outputDirectory;

    public DiffCollector(IGitClient gitClient, IFileSystemService fileSystemService, string outputDirectory)
    {
        _gitClient = gitClient;
        _fileSystemService = fileSystemService;
        _outputDirectory = outputDirectory;
    }

    // Persists original content and diffs for changed files into the output directory.
    public async Task CollectAsync(IReadOnlyList<string> changedFiles, CancellationToken cancellationToken = default)
    {
        foreach (var file in changedFiles)
        {
            var targetPath = Path.Combine(_outputDirectory, file);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string content = "";

            try
            {
                var original = await _gitClient.ShowFileFromHeadAsync(file, cancellationToken).ConfigureAwait(false);
                var diff = await _gitClient.DiffFileFromHeadAsync(file, cancellationToken).ConfigureAwait(false);

                content = $"FILE: {file}\n\n----- ORIGINAL (HEAD) -----\n{original}\n----- DIFF -----\n{diff}";
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("exists on disk, but not in 'HEAD'", StringComparison.OrdinalIgnoreCase) ||
                                                       ex.Message.Contains("Path", StringComparison.OrdinalIgnoreCase) &&
                                                       ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("$New file {file}");
                var current = await _fileSystemService.ReadFileAsync(file, cancellationToken).ConfigureAwait(false);
                content = $"FILE: {file}\n\n----- ORIGINAL (HEAD) -----\n<file does not exist in HEAD>\n----- CURRENT CONTENT -----\n{current}";
            }

            await _fileSystemService.WriteFileAsync(targetPath, content, cancellationToken).ConfigureAwait(false);
        }
    }
}
