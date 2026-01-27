namespace ReviewApp.Infrastructure;

public interface IContentDownloader
{
    // Downloads the content at the given URL as a string.
    Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default);
}

public class CurlDownloader : IContentDownloader
{
    private readonly IProcessRunner _processRunner;

    public CurlDownloader(IProcessRunner processRunner)
        => _processRunner = processRunner;

    // Retrieves remote content via HttpClient to avoid shelling out to curl.
    public async Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync("curl", $"-fsSL \"{url}\"", cancellationToken: cancellationToken);

        return result.StandardOutput;
    }
}
