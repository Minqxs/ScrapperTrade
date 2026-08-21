using Microsoft.EntityFrameworkCore;

namespace ScrapperTrade.Infrastructure.Persistence;

public static class PersistenceBootstrap
{
    public static DbContextOptions<ScrapperTradeDbContext> CreateOptions(string? databasePath = null) =>
        new DbContextOptionsBuilder<ScrapperTradeDbContext>()
            .UseSqlite(LocalDataPath.BuildConnectionString(databasePath))
            .Options;

    public static async Task MigrateAsync(string? databasePath = null, CancellationToken cancellationToken = default)
    {
        await using var db = new ScrapperTradeDbContext(CreateOptions(databasePath));
        await db.Database.MigrateAsync(cancellationToken);
    }
}
