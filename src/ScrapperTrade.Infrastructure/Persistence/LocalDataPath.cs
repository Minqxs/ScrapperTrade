namespace ScrapperTrade.Infrastructure.Persistence;

public static class LocalDataPath
{
    public static string GetDatabasePath(string? localApplicationData = null)
    {
        var explicitDataDirectory = Environment.GetEnvironmentVariable("SCRAPPERTRADE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(localApplicationData) && !string.IsNullOrWhiteSpace(explicitDataDirectory))
            return Path.Combine(Path.GetFullPath(explicitDataDirectory), "scrappertrade.db");

        var root = localApplicationData;
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("The per-user local application data directory is unavailable.");

        return Path.Combine(Path.GetFullPath(root), "ScrapperTrade", "data", "scrappertrade.db");
    }

    public static string BuildConnectionString(string? databasePath = null)
    {
        var path = Path.GetFullPath(databasePath ?? GetDatabasePath());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return $"Data Source={path};Cache=Shared;Foreign Keys=True";
    }
}
