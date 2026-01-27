namespace ReviewApp.Core.Abstractions;

public interface IContentDownloader
{
    // Downloads the content at the given URL as a string.
    Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default);
}
