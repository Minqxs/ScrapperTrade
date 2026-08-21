using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScrapperTrade.Infrastructure.Persistence;
using Xunit;

namespace ScrapperTrade.Infrastructure.Tests;

public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ScrapperTrade.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "test.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        return Task.CompletedTask;
    }

    [Fact]
    public void Default_path_is_beneath_per_user_local_application_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "user-local");
        var path = LocalDataPath.GetDatabasePath(root);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "ScrapperTrade", "data", "scrappertrade.db"), path);
    }

    [Fact]
    public async Task Migration_creates_required_schema_and_records_history()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath);
        await using var db = CreateDb();
        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();

        Assert.Contains("configuration", tables);
        Assert.Contains("instruments", tables);
        Assert.Contains("audit_logs", tables);
        Assert.Contains("system_events", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Configuration_and_instrument_upserts_are_deterministic()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath);
        await using var db = CreateDb();
        var now = DateTimeOffset.Parse("2026-08-21T12:00:00+02:00");
        var configuration = new ConfigurationRepository(db);
        await configuration.SetAsync("system.mode", "PAUSED", now);
        await configuration.SetAsync("system.mode", "RUNNING", now.AddMinutes(1));
        Assert.Equal("RUNNING", (await configuration.FindAsync("system.mode"))!.Value);
        Assert.Equal(1, await db.Configuration.CountAsync());

        var instruments = new InstrumentRepository(db);
        await instruments.UpsertAsync(new InstrumentRecord { LogicalSymbol = "xauusd", Enabled = false, UpdatedAt = now });
        await instruments.UpsertAsync(new InstrumentRecord { LogicalSymbol = "XAUUSD", Enabled = true, BrokerSymbol = "XAUUSDm", UpdatedAt = now.AddMinutes(1) });
        var saved = Assert.Single(await instruments.ListAsync());
        Assert.True(saved.Enabled);
        Assert.Equal("XAUUSDm", saved.BrokerSymbol);
    }

    [Fact]
    public async Task Audit_and_system_events_are_append_only_and_newest_first()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath);
        await using var db = CreateDb();
        var repository = new EventRepository(db);
        var first = DateTimeOffset.Parse("2026-08-21T10:00:00Z");
        await repository.AppendAuditAsync(new AuditLogRecord { OccurredAt = first, Category = "RISK", Action = "ORDER_REJECTED", Outcome = "DENIED", Detail = "DEMO gate failed" });
        await repository.AppendAuditAsync(new AuditLogRecord { OccurredAt = first.AddSeconds(1), Category = "RISK", Action = "ORDER_REJECTED", Outcome = "DENIED", Detail = "Emergency locked" });
        await repository.AppendSystemEventAsync(new SystemEventRecord { OccurredAt = first, Severity = "INFO", EventType = "STARTING", Detail = "Host starting" });

        Assert.Equal("Emergency locked", (await repository.ReadAuditAsync())[0].Detail);
        Assert.Single(await repository.ReadSystemEventsAsync());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ReadAuditAsync(0));
    }

    private ScrapperTradeDbContext CreateDb() => new(PersistenceBootstrap.CreateOptions(DatabasePath));
}
