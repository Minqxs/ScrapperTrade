namespace ScrapperTrade.Infrastructure.Persistence;

public static class LocalDataPath
{
    public static string GetDatabasePath(string? localApplicationData = null)
    {
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
