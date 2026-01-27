using System.Text.Json;

namespace ReviewApp.Infrastructure;

public class ConfigReader
{
    public static async Task<AppConfig> Read(string jsonPath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        var appConfig = JsonSerializer.Deserialize<AppConfig>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (appConfig is null)
        {
            throw new InvalidOperationException("Failed to deserialize app configuration from appsettings.local.json");
        }

        return appConfig;
    }
}
