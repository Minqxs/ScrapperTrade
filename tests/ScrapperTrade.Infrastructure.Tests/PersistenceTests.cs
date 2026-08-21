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
        Assert.Contains("broker_symbol_metadata", tables);
        Assert.Contains("exposure_groups", tables);
        Assert.Contains("risk_policy_versions", tables);
        Assert.Contains("risk_policy_changes", tables);
        Assert.Contains("positions", tables);
        Assert.Contains("trade_events", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Trading_universe_preserves_permissions_and_broker_metadata()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath);
        await using var db = CreateDb();
        var now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");
        var instruments = new InstrumentRepository(db);
        var instrument = new InstrumentRecord { LogicalSymbol = "nas100", Enabled = true, AllowLong = true, AllowShort = false, MaxConcurrentPositions = 2, ExposureGroupCode = "US_TECH", TradingSessionsJson = "[{\"day\":\"Friday\"}]", UpdatedAt = now };
        await instruments.UpsertAsync(instrument);
        var universe = new TradingUniverseRepository(db);
        await universe.UpsertExposureGroupAsync(new ExposureGroupRecord { Code = "us_tech", DisplayName = "US technology indices", MaxRiskFraction = .01m, MaxSameDirectionRiskFraction = .005m, UpdatedAt = now });
        await universe.UpsertBrokerMetadataAsync(new BrokerSymbolMetadataRecord { InstrumentId = instrument.Id, BrokerSymbol = "USTECm", TickSize = .1m, TickValue = 1m, ContractSize = 1m, VolumeMin = .01m, VolumeMax = 10m, VolumeStep = .01m, ObservedAt = now });

        var saved = Assert.Single(await instruments.ListAsync());
        Assert.False(saved.AllowShort);
        Assert.Equal("US_TECH", saved.ExposureGroupCode);
        Assert.Equal("USTECm", (await db.BrokerSymbolMetadata.AsNoTracking().SingleAsync()).BrokerSymbol);
    }

    [Fact]
    public async Task Risk_policy_versions_are_sequential_and_change_audited()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath);
        await using var db = CreateDb();
        var repository = new RiskPolicyRepository(db);
        var now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");
        await repository.AddVersionAsync(new RiskPolicyVersionRecord { Version = 1, PolicyJson = "{\"maxRisk\":0.005}", ChangedBy = "USER", ChangeReason = "Initial hard policy", EffectiveAt = now });
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddVersionAsync(new RiskPolicyVersionRecord { Version = 3, PolicyJson = "{}", ChangedBy = "USER", ChangeReason = "Skip", EffectiveAt = now }));

        Assert.Equal(1, (await repository.GetCurrentAsync())!.Version);
        var change = await db.RiskPolicyChanges.AsNoTracking().SingleAsync();
        Assert.Equal(0, change.FromVersion);
        Assert.Equal(1, change.ToVersion);
        Assert.Equal("USER", change.ChangedBy);
    }

    [Fact]
    public async Task Positions_and_trade_events_preserve_reconciliation_evidence()
    {
        await PersistenceBootstrap.MigrateAsync(DatabasePath);
        await using var db = CreateDb();
        var repository = new PositionRepository(db);
        var position = new PositionRecord { BrokerPositionId = "12345", LogicalSymbol = "XAUUSD", BrokerSymbol = "XAUUSDm", Side = "BUY", EntryPrice = 2000, StopPrice = 1990, Volume = .1m, RiskAmount = 100, ExposureGroupCode = "METALS", Status = "OPEN", OpenedAt = DateTimeOffset.Parse("2026-08-21T12:00:00Z") };
        await repository.AddAsync(position);
        await repository.AppendEventAsync(new TradeEventRecord { PositionId = position.Id, SignalId = Guid.NewGuid(), EventType = "POSITION_RECONCILED", DetailJson = "{\"source\":\"MT5\"}", OccurredAt = position.OpenedAt });

        Assert.Single(await repository.ListOpenAsync());
        Assert.Equal(position.Id, (await db.TradeEvents.AsNoTracking().SingleAsync()).PositionId);
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
